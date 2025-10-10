.PHONY: help start stop setup clean logs test rebuild dev start-openfga restore update-store-id

help:
	@echo "Available commands:"
	@echo ""
	@echo "🐳 Docker Commands:"
	@echo "  make start         - Start all services (Docker)"
	@echo "  make stop          - Stop all services"
	@echo "  make setup         - Setup OpenFGA (run after first start)"
	@echo "  make clean         - Remove all containers and volumes"
	@echo "  make logs          - View logs from all services"
	@echo "  make rebuild       - Rebuild and restart API"
	@echo ""
	@echo "💻 Local Development Commands:"
	@echo "  make start-openfga - Start only OpenFGA in Docker"
	@echo "  make dev           - Run Azure Functions locally"
	@echo "  make restore       - Restore NuGet packages"
	@echo "  make update-store-id - Update local.settings.json with store ID from .env"
	@echo ""
	@echo "🧪 Testing Commands:"
	@echo "  make test          - Test the API endpoints"
	@echo "  make open          - Open the frontend in browser"

start:
	@echo "Starting services..."
	docker-compose up -d
	@echo "Waiting for services to be ready..."
	@sleep 5
	@echo "Services started! Run 'make setup' if this is your first time."

stop:
	@echo "Stopping services..."
	docker-compose down

setup:
	@echo "Setting up OpenFGA..."
	./scripts/setup-openfga.sh
	@echo "Setup complete! Run 'make open' to view the app."

clean:
	@echo "Cleaning up..."
	docker-compose down -v
	rm -f .env
	@echo "Cleanup complete!"

logs:
	docker-compose logs -f

test:
	@echo "Testing API endpoints..."
	@echo "\n1. Testing Alice (Admin):"
	@curl -s "http://localhost:7071/api/menu?user=alice" | jq
	@echo "\n2. Testing Bob (Viewer):"
	@curl -s "http://localhost:7071/api/menu?user=bob" | jq
	@echo "\n3. Testing Charlie (Editor):"
	@curl -s "http://localhost:7071/api/menu?user=charlie" | jq

rebuild:
	@echo "Rebuilding API..."
	docker-compose build api
	docker-compose up -d api
	@echo "API rebuilt and restarted!"

open:
	@echo "Opening frontend..."
	@open frontend/index.html || xdg-open frontend/index.html || start frontend/index.html

# Quick start - runs everything
quickstart: start
	@sleep 10
	@make setup
	@make open
	@echo "\nApp is running!"
	@echo "- Frontend: See browser"
	@echo "- API: http://localhost:7071/api/menu"
	@echo "- OpenFGA Playground: http://localhost:3000"

# Local development commands
start-openfga:
	@echo "Starting OpenFGA..."
	docker-compose up openfga -d
	@echo "Waiting for OpenFGA to be ready..."
	@sleep 5
	@echo "OpenFGA is running on http://localhost:8080"

restore:
	@echo "Restoring NuGet packages..."
	cd backend/MenuApi && dotnet restore

update-store-id:
	@if [ ! -f .env ]; then \
		echo "Error: .env file not found. Run 'make setup' first."; \
		exit 1; \
	fi
	@STORE_ID=$$(grep OPENFGA_STORE_ID .env | cut -d '=' -f2); \
	if [ -z "$$STORE_ID" ]; then \
		echo "Error: OPENFGA_STORE_ID not found in .env"; \
		exit 1; \
	fi; \
	echo "Updating local.settings.json with Store ID: $$STORE_ID"; \
	cd backend/MenuApi && \
	jq ".Values.OPENFGA_STORE_ID = \"$$STORE_ID\"" local.settings.json > local.settings.tmp.json && \
	mv local.settings.tmp.json local.settings.json
	@echo "✓ Store ID updated in local.settings.json"

dev: restore update-store-id
	@echo "Starting Azure Functions locally..."
	@echo "Make sure OpenFGA is running (run 'make start-openfga' if not)"
	@echo ""
	cd backend/MenuApi && func start

# Quick local development start
quickstart-local: start-openfga
	@sleep 10
	@make setup
	@echo "\n🚀 Starting Azure Functions locally..."
	@make dev
