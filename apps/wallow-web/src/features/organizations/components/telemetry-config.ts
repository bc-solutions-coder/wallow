import type { TelemetryConfigurationDto } from "@bc-solutions-coder/sdk";

export function telemetryEnvBlock(config: TelemetryConfigurationDto): string {
  return [
    `WALLOW_TELEMETRY_ENDPOINT=${config.endpoint}`,
    `WALLOW_TELEMETRY_CREDENTIAL=${config.credential}`,
    `WALLOW_TELEMETRY_ENVIRONMENT=${config.environment}`,
    `WALLOW_TELEMETRY_RELEASE=${config.release}`,
  ].join("\n");
}

export function telemetryStatusLabel(status: string): string {
  switch (status) {
    case "active": {
      return "Active";
    }
    case "action-required": {
      return "Action required";
    }
    case "pending-rotation": {
      return "Pending rotation";
    }
    case "pending-revocation": {
      return "Pending revocation";
    }
    case "disabled": {
      return "Disabled";
    }
    default: {
      return "Pending";
    }
  }
}
