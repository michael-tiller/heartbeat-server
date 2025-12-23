// API client for Heartbeat server using generated OpenAPI client
import { Configuration, HealthApi, UsersApi } from '../../../contracts/generated/mobile';
import type {
  HeartbeatContractsHealthResponse,
  HeartbeatContractsRegisterRequest,
  HeartbeatContractsRegisterResponse,
} from '../../../contracts/generated/mobile/models';

const API_BASE_URL = process.env.API_BASE_URL ?? "http://10.0.2.2:5166";

// Re-export types for convenience (using generated types directly)
export type {
  HeartbeatContractsHealthResponse as HealthResponse,
  HeartbeatContractsRegisterRequest as RegisterRequest,
  HeartbeatContractsRegisterResponse as RegisterResponse,
};

class ApiClient {
  private config: Configuration;
  private healthApi: HealthApi;
  private usersApi: UsersApi;

  constructor(baseUrl: string = API_BASE_URL) {
    this.config = new Configuration({
      basePath: baseUrl,
    });
    this.healthApi = new HealthApi(this.config);
    this.usersApi = new UsersApi(this.config);
  }

  async checkHealth(): Promise<HeartbeatContractsHealthResponse> {
    try {
      const response = await this.healthApi.checkHealthRaw();
      return await response.value();
    } catch (error: any) {
      const errorText = error?.raw ? await error.raw.text() : error?.message;
      throw new Error(errorText || `HTTP ${error?.raw?.status || 'Unknown error'}`);
    }
  }

  async register(request: { deviceId: string }): Promise<HeartbeatContractsRegisterResponse> {
    try {
      const response = await this.usersApi.registerRaw({
        heartbeatContractsRegisterRequest: {
          deviceId: request.deviceId,
        },
      });
      return await response.value();
    } catch (error: any) {
      const errorText = error?.raw ? await error.raw.text() : error?.message;
      throw new Error(errorText || `HTTP ${error?.raw?.status || 'Unknown error'}`);
    }
  }
}

// Export singleton instance
export const apiClient = new ApiClient();

// Export class for custom instances if needed
export { ApiClient };
