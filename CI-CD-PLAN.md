# CI/CD Implementation Plan for Menu App with OpenFGA

## 🎯 Project Overview

**Architecture:**
- **Frontend**: Azure Static Web Apps (React/Vite)
- **Backend**: Azure Container Apps (.NET Functions + Custom OpenFGA)
- **Database**: Azure SQL Database (shared for OpenFGA + app data)
- **Auth**: OpenFGA for fine-grained authorization

---

## 📁 Repository Structure

```
menu-app/
├── .github/
│   └── workflows/
│       ├── frontend-ci-cd.yml          # Frontend deployment
│       ├── backend-ci-cd.yml           # Backend deployment
│       ├── database-migrations.yml     # DB schema updates
│       └── pr-validation.yml           # PR checks
├── frontend/                           # React/Vite app
│   ├── src/
│   ├── public/
│   ├── package.json
│   ├── vite.config.ts
│   └── index.html
├── backend/
│   └── MenuApi/                        # .NET Azure Functions
│       ├── MenuApi.csproj
│       ├── Program.cs
│       ├── MenuFunction.cs
│       ├── Models/
│       ├── Data/
│       └── start.sh
├── openfga-fork/                       # Custom OpenFGA with SQL Server
│   ├── pkg/storage/sqlserver/
│   └── assets/migrations/sqlserver/
├── database/
│   ├── app-schema.sql                  # Application schema
│   └── migrations/                     # EF Core migrations
├── infrastructure/                      # IaC (Bicep/Terraform)
│   ├── main.bicep
│   ├── parameters.json
│   └── modules/
├── Dockerfile.combined                  # Backend + OpenFGA
├── docker-compose.yml                   # Local development
└── README.md
```

---

## 🔄 CI/CD Workflows

### **1. Frontend CI/CD Pipeline** (`frontend-ci-cd.yml`)

**Triggers:**
- Push to `main` branch (paths: `frontend/**`)
- Pull requests to `main` (validation only)
- Manual workflow dispatch

**Steps:**

#### **Build & Test**
```yaml
- Checkout code
- Setup Node.js (v20)
- Install dependencies (npm ci)
- Run linting (ESLint)
- Run type checking (TypeScript)
- Run unit tests (Vitest)
- Build production bundle (vite build)
- Upload artifacts
```

#### **Deploy to Azure Static Web Apps**
```yaml
Environments:
  - Development (auto-deploy from `develop` branch)
  - Staging (auto-deploy from `main` branch)
  - Production (manual approval required)

- Deploy to Azure Static Web Apps
- Run E2E tests (Playwright) against deployed URL
- Health check (verify app loads)
```

**Key Features:**
- Preview deployments for PRs
- Automatic staging slot deployment
- Manual approval for production
- Rollback capability

---

### **2. Backend CI/CD Pipeline** (`backend-ci-cd.yml`)

**Triggers:**
- Push to `main` branch (paths: `backend/**`, `openfga-fork/**`, `Dockerfile.combined`)
- Pull requests to `main` (build only)
- Manual workflow dispatch

**Steps:**

#### **Build Custom OpenFGA + .NET API**
```yaml
- Checkout code
- Setup Docker Buildx
- Login to Azure Container Registry (ACR)
- Build multi-platform image (linux/amd64)
  * Stage 1: Build custom OpenFGA (Go)
  * Stage 2: Build .NET Functions
  * Stage 3: Combine into final image
- Run security scan (Trivy/Snyk)
- Push to ACR with tags:
  * latest
  * git-{sha}
  * v{version} (on release)
```

#### **Deploy to Azure Container Apps**
```yaml
Environments:
  - Development (auto-deploy)
  - Staging (auto-deploy)
  - Production (manual approval)

- Update Azure Container App revision
- Set environment variables:
  * OPENFGA_DATASTORE_URI (from Key Vault)
  * DOTNET_CONNECTION_STRING (from Key Vault)
  * OPENFGA_STORE_ID
- Run database migrations (OpenFGA + EF Core)
- Health check (verify APIs respond)
- Run integration tests
- Enable traffic to new revision (blue-green deployment)
```

**Key Features:**
- Image caching for faster builds
- Multi-stage build optimization
- Automatic rollback on health check failure
- Blue-green deployment strategy

---

### **3. Database Migrations Pipeline** (`database-migrations.yml`)

**Triggers:**
- Push to `main` branch (paths: `database/**`, `openfga-fork/assets/migrations/sqlserver/**`)
- Manual workflow dispatch (for production)

**Steps:**

#### **OpenFGA Migrations**
```yaml
- Checkout code
- Build custom OpenFGA binary
- Run: openfga migrate --datastore-engine sqlserver
- Verify migration success
```

#### **Application Migrations (EF Core)**
```yaml
- Checkout code
- Setup .NET 8
- Install EF Core tools
- Generate SQL script: dotnet ef migrations script
- Review changes (create PR for review)
- Apply migrations (manual approval for prod)
```

**Key Features:**
- Dry-run mode for validation
- Automatic rollback on failure
- Migration history tracking
- Manual approval for production

---

### **4. PR Validation Pipeline** (`pr-validation.yml`)

**Triggers:**
- Pull requests to `main` or `develop`

**Checks:**

#### **Frontend**
```yaml
- Code quality (ESLint, Prettier)
- Type checking (TypeScript)
- Unit tests (Vitest)
- Build validation
- Bundle size analysis
```

#### **Backend**
```yaml
- Code quality (dotnet format)
- Unit tests (.NET Test)
- Docker build validation
- Security scan (SAST)
```

#### **Database**
```yaml
- Migration validation
- Schema comparison
- Breaking change detection
```

**Status Checks:**
- All checks must pass before merge
- Code coverage threshold (80%)
- No critical security vulnerabilities

---

## 🏗️ Infrastructure as Code (IaC)

### **Azure Bicep Modules**

**Structure:**
```
infrastructure/
├── main.bicep                          # Main orchestration
├── parameters.dev.json
├── parameters.staging.json
├── parameters.prod.json
└── modules/
    ├── container-registry.bicep        # ACR
    ├── container-apps.bicep            # Backend container
    ├── static-web-app.bicep            # Frontend
    ├── sql-database.bicep              # Azure SQL
    ├── key-vault.bicep                 # Secrets
    ├── log-analytics.bicep             # Monitoring
    └── app-insights.bicep              # Application Insights
```

**Deployment Pipeline:**
```yaml
- Validate Bicep syntax
- Run what-if analysis
- Manual approval (staging/prod)
- Deploy infrastructure
- Export outputs (URLs, connection strings)
- Store secrets in Key Vault
```

---

## 🌍 Environments

### **1. Development**
- **Frontend**: Auto-deploy from `develop` branch
- **Backend**: Auto-deploy from `develop` branch
- **Database**: Azure SQL Database (Free tier)
- **Purpose**: Active development, integration testing
- **URL**: `https://dev-menu-app.azurestaticapps.net`

### **2. Staging**
- **Frontend**: Auto-deploy from `main` branch
- **Backend**: Auto-deploy from `main` branch
- **Database**: Azure SQL Database (shared with dev)
- **Purpose**: Pre-production testing, QA
- **URL**: `https://staging-menu-app.azurestaticapps.net`

### **3. Production**
- **Frontend**: Manual approval required
- **Backend**: Manual approval required
- **Database**: Azure SQL Database (dedicated)
- **Purpose**: Live production environment
- **URL**: `https://menu-app.azurestaticapps.net`

---

## 🔐 Secrets Management

**Azure Key Vault Secrets:**
```
- SQL-CONNECTION-STRING
- SQL-ADMIN-PASSWORD
- OPENFGA-DATASTORE-URI
- GITHUB-PAT (for deployments)
- ACR-USERNAME
- ACR-PASSWORD
```

**GitHub Secrets Required:**
```
- AZURE_CREDENTIALS (Service Principal)
- AZURE_SUBSCRIPTION_ID
- AZURE_RESOURCE_GROUP
- AZURE_ACR_NAME
- AZURE_KEY_VAULT_NAME
```

**Access Pattern:**
- GitHub Actions → Azure Key Vault (via Service Principal)
- Container Apps → Key Vault (via Managed Identity)
- Never hardcode secrets in workflows

---

## 🧪 Testing Strategy

### **Frontend Tests**
1. **Unit Tests** (Vitest) - Run on every commit
2. **Component Tests** (React Testing Library) - Run on every commit
3. **E2E Tests** (Playwright) - Run on deploy to staging
4. **Visual Regression** (Percy/Chromatic) - Run on PR

### **Backend Tests**
1. **Unit Tests** (xUnit) - Run on every commit
2. **Integration Tests** (TestContainers) - Run on PR
3. **API Tests** (Postman/Newman) - Run on deploy
4. **Load Tests** (k6) - Run nightly on staging

### **Database Tests**
1. **Migration Tests** - Validate up/down migrations
2. **Data Integrity** - Verify constraints and relationships
3. **Performance Tests** - Query optimization checks

---

## 📊 Monitoring & Observability

### **Application Insights**
- Request tracking
- Exception monitoring
- Custom events (authorization decisions)
- Performance metrics
- User analytics

### **Log Analytics**
- Container logs
- OpenFGA audit logs
- Database query logs
- Aggregated dashboards

### **Alerts**
- API response time > 2s
- Error rate > 5%
- Database CPU > 80%
- Container restart
- Failed deployments

---

## 🚀 Deployment Strategy

### **Blue-Green Deployment**
```
1. Deploy new revision (green)
2. Run health checks
3. Run smoke tests
4. Shift 10% traffic to green
5. Monitor for 5 minutes
6. Shift 100% traffic if healthy
7. Keep blue for 24h (rollback capability)
8. Decommission blue revision
```

### **Rollback Procedure**
```
- Automatic: Health check failure triggers rollback
- Manual: One-click rollback via GitHub Actions
- Database: Maintain backward compatibility for 1 release
```

---

## 🔄 Branch Strategy

### **GitFlow Model**

```
main (production)
  ├── develop (staging)
  │   ├── feature/menu-permissions
  │   ├── feature/user-dashboard
  │   └── bugfix/auth-token-refresh
  └── hotfix/critical-security-patch
```

**Rules:**
- `main`: Production-ready code only
- `develop`: Integration branch for features
- `feature/*`: New features (branch from develop)
- `bugfix/*`: Non-critical fixes (branch from develop)
- `hotfix/*`: Critical fixes (branch from main)

**Merge Strategy:**
- Feature → Develop: Squash merge + PR review
- Develop → Main: Merge commit (preserves history)
- Hotfix → Main: Merge commit + cherry-pick to develop

---

## 📋 CI/CD Checklist

### **Phase 1: Foundation (Week 1)**
- [ ] Setup Azure Container Registry
- [ ] Create Azure Container Apps (dev environment)
- [ ] Setup Azure Static Web Apps (dev environment)
- [ ] Configure Azure Key Vault
- [ ] Create Service Principal for GitHub Actions

### **Phase 2: Backend Pipeline (Week 2)**
- [ ] Create `backend-ci-cd.yml` workflow
- [ ] Implement Docker build with caching
- [ ] Setup automated migrations
- [ ] Add health checks
- [ ] Test deployment to dev environment

### **Phase 3: Frontend Pipeline (Week 2)**
- [ ] Migrate index.html to React/Vite project
- [ ] Create `frontend-ci-cd.yml` workflow
- [ ] Setup preview deployments for PRs
- [ ] Add E2E tests with Playwright
- [ ] Test deployment to dev environment

### **Phase 4: Infrastructure (Week 3)**
- [ ] Create Bicep modules for all resources
- [ ] Implement IaC deployment workflow
- [ ] Setup staging environment
- [ ] Setup production environment
- [ ] Configure environment variables

### **Phase 5: Testing & Quality (Week 4)**
- [ ] Add unit tests (frontend + backend)
- [ ] Setup integration tests
- [ ] Configure code coverage reporting
- [ ] Add security scanning (SAST/DAST)
- [ ] Performance testing

### **Phase 6: Observability (Week 4)**
- [ ] Configure Application Insights
- [ ] Setup Log Analytics workspace
- [ ] Create monitoring dashboards
- [ ] Configure alerts
- [ ] Setup automated incident response

### **Phase 7: Production Readiness (Week 5)**
- [ ] Manual approval workflows
- [ ] Rollback procedures
- [ ] Disaster recovery plan
- [ ] Documentation
- [ ] Team training

---

## 💰 Cost Optimization

### **Azure Services (Estimated Monthly Cost)**

**Development Environment:**
- Azure SQL Database (Free tier): **$0**
- Azure Container Apps (0.5 vCPU): **~$15**
- Azure Static Web Apps (Free tier): **$0**
- Azure Container Registry (Basic): **$5**
- **Total: ~$20/month**

**Production Environment:**
- Azure SQL Database (2 vCores): **~$200**
- Azure Container Apps (2 vCPU, HA): **~$120**
- Azure Static Web Apps (Standard): **$9**
- Azure Container Registry (Standard): **$20**
- Application Insights: **~$10**
- **Total: ~$359/month**

**Cost Optimization Strategies:**
- Use Azure Reserved Instances (save 30-50%)
- Auto-scale containers based on load
- Use Azure DevTest pricing for non-prod
- Implement proper caching (reduce DB load)

---

## 🎓 Best Practices

### **Security**
- ✅ Never commit secrets to git
- ✅ Use Managed Identities where possible
- ✅ Implement least-privilege access
- ✅ Regular security scanning
- ✅ Automated dependency updates (Dependabot)

### **Performance**
- ✅ Docker layer caching
- ✅ CDN for static assets
- ✅ Database connection pooling
- ✅ API response caching
- ✅ Image optimization

### **Reliability**
- ✅ Health checks for all services
- ✅ Automatic retry logic
- ✅ Circuit breakers
- ✅ Rate limiting
- ✅ Graceful degradation

### **Maintainability**
- ✅ Infrastructure as Code
- ✅ Automated testing at all levels
- ✅ Comprehensive logging
- ✅ Clear documentation
- ✅ Code review process

---

## 📖 Documentation

### **Required Docs**
1. **Architecture Diagram** (draw.io/Mermaid)
2. **API Documentation** (OpenAPI/Swagger)
3. **Deployment Runbook**
4. **Incident Response Plan**
5. **Developer Onboarding Guide**

---

## 🔮 Future Enhancements

### **Phase 2 Features**
- [ ] Multi-region deployment
- [ ] Feature flags (LaunchDarkly/Azure App Config)
- [ ] A/B testing framework
- [ ] Advanced caching (Redis)
- [ ] GraphQL API layer
- [ ] Mobile app deployment (React Native)
- [ ] Automated performance testing
- [ ] Chaos engineering (Azure Chaos Studio)

---

## 📞 Support & Escalation

**Deployment Issues:**
1. Check GitHub Actions logs
2. Review Application Insights
3. Check container logs in Azure Portal
4. Rollback if necessary

**Database Issues:**
1. Check migration logs
2. Verify connection strings
3. Review Azure SQL metrics
4. Restore from backup if needed

---

## ✅ Success Metrics

**Deployment Metrics:**
- Deployment frequency: > 10/day (dev), > 1/day (prod)
- Lead time for changes: < 1 hour
- Mean time to recovery: < 30 minutes
- Change failure rate: < 5%

**Application Metrics:**
- API response time: < 500ms (p95)
- Uptime: > 99.9%
- Error rate: < 1%
- Database query time: < 100ms (p95)

---

**This CI/CD plan provides a complete, production-ready pipeline for your Menu App!** 🚀

Would you like me to start implementing any specific workflow?
