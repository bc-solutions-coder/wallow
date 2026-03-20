# Wallow Grafana Dashboards

Sample Grafana dashboards for monitoring the Wallow platform.

## Dashboards

| Dashboard | Datasource | Description |
|-----------|------------|-------------|
| `usage-dashboard.json` | Prometheus | API usage metrics, request rates, latency, and error rates |
| `billing-dashboard.json` | PostgreSQL | Invoice management, revenue tracking, and payment analytics |

## Prerequisites

Before importing these dashboards, ensure you have:

1. **Prometheus Datasource** - configured to scrape your Wallow API metrics
2. **PostgreSQL Datasource** - configured to connect to your Wallow database

## Import Instructions

### Via Grafana UI

1. Log into your Grafana instance
2. Navigate to **Dashboards** > **Import** (or use the `+` icon)
3. Click **Upload JSON file** and select the dashboard JSON file
4. Select the appropriate datasource when prompted:
   - For `usage-dashboard.json`: Select your Prometheus datasource
   - For `billing-dashboard.json`: Select your PostgreSQL datasource
5. Click **Import**

### Via Grafana API

```bash
# Import usage dashboard
curl -X POST \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d @usage-dashboard.json \
  http://localhost:3000/api/dashboards/db

# Import billing dashboard
curl -X POST \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d @billing-dashboard.json \
  http://localhost:3000/api/dashboards/db
```

### Via Provisioning

To automatically provision these dashboards, add them to your Grafana provisioning configuration:

```yaml
# /etc/grafana/provisioning/dashboards/wallow.yaml
apiVersion: 1

providers:
  - name: 'Wallow'
    orgId: 1
    folder: 'Wallow'
    folderUid: 'wallow'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    options:
      path: /var/lib/grafana/dashboards/wallow
```

Copy the JSON files to `/var/lib/grafana/dashboards/wallow/`.

## Tenant Variable

Both dashboards include a `$tenant_id` variable that filters all queries by tenant. This supports Wallow's multi-tenancy model.

### Usage Dashboard Variables

| Variable | Type | Description |
|----------|------|-------------|
| `DS_PROMETHEUS` | Datasource | Prometheus datasource selector |
| `tenant_id` | Query | Populated from `http_server_request_duration_seconds_count` labels |

### Billing Dashboard Variables

| Variable | Type | Description |
|----------|------|-------------|
| `DS_POSTGRESQL` | Datasource | PostgreSQL datasource selector |
| `tenant_id` | Query | Populated from `billing.invoices` table |

## Embedding Dashboards

To embed dashboards in your application with tenant filtering:

```
/d/wallow-usage?var-tenant_id=${currentTenantId}&kiosk=tv
/d/wallow-billing?var-tenant_id=${currentTenantId}&kiosk=tv
```

See `docs/grafana/KEYCLOAK_OAUTH_SETUP.md` for full Grafana embedding and OAuth configuration.

## Customization

### Adding Panels

1. Open the dashboard in Grafana
2. Click **Add** > **Visualization**
3. Configure your query using the `$tenant_id` variable
4. Save the dashboard
5. Export via **Dashboard Settings** > **JSON Model** to update the file

### Query Examples

**Prometheus (Usage)**
```promql
# Request rate by endpoint
sum(rate(http_server_request_duration_seconds_count{tenant_id="$tenant_id"}[5m])) by (endpoint)

# P95 latency
histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{tenant_id="$tenant_id"}[5m])) by (le))
```

**PostgreSQL (Billing)**
```sql
-- Revenue trend
SELECT
  date_trunc('day', paid_at) as time,
  SUM(amount) as revenue
FROM billing.payments
WHERE tenant_id = '$tenant_id'
  AND paid_at BETWEEN $__timeFrom() AND $__timeTo()
GROUP BY 1
ORDER BY 1

-- Invoices by status
SELECT status, COUNT(*) as count, SUM(total_amount) as total
FROM billing.invoices
WHERE tenant_id = '$tenant_id'
  AND created_at BETWEEN $__timeFrom() AND $__timeTo()
GROUP BY status
```

## Metrics Reference

### Usage Dashboard Metrics

These metrics are collected via OpenTelemetry instrumentation:

| Metric | Type | Description |
|--------|------|-------------|
| `http_server_request_duration_seconds` | Histogram | HTTP request latency |
| `http_server_request_duration_seconds_count` | Counter | Total HTTP requests |

Labels: `tenant_id`, `endpoint`, `http_method`, `http_status_code`

### Billing Dashboard Tables

| Table | Key Columns |
|-------|-------------|
| `billing.invoices` | `tenant_id`, `invoice_number`, `customer_name`, `total_amount`, `status`, `created_at`, `due_date` |
| `billing.payments` | `tenant_id`, `amount`, `paid_at` |

## Support

For issues or feature requests, see the main project documentation:
- Architecture: `docs/plans/2026-02-04-wallow-pivot-design.md`
- Grafana OAuth: `docs/grafana/KEYCLOAK_OAUTH_SETUP.md`
