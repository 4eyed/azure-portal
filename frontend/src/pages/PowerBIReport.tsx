import { useParams } from 'react-router-dom';
import { PowerBIEmbed } from '../components/PowerBI/PowerBIEmbed';
import './PowerBIReport.css';

export function PowerBIReport() {
  const { reportId } = useParams();

  if (!reportId) {
    return <div className="error">Report ID is required</div>;
  }

  return (
    <div className="powerbi-report-page">
      <PowerBIEmbed reportId={reportId} />
    </div>
  );
}
