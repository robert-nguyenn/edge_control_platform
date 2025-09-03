import fetch from 'node-fetch';
import { createHash } from 'crypto';

export interface EdgeControlConfig {
  baseUrl: string;
  apiKey?: string;
  pollIntervalMs?: number;
  defaultDecisions?: Record<string, boolean>;
  onUpdate?: (flags: Record<string, FlagData>) => void;
  onError?: (error: Error) => void;
}

export interface FlagData {
  key: string;
  description: string;
  rolloutPercent: number;
  rules: any;
  etag?: string;
}

export interface Context {
  userId: string;
  [key: string]: any;
}

export class EdgeControlClient {
  private config: Required<EdgeControlConfig>;
  private flags: Record<string, FlagData> = {};
  private etags: Record<string, string> = {};
  private pollTimer?: NodeJS.Timeout;
  private isPolling = false;

  constructor(config: EdgeControlConfig) {
    this.config = {
      apiKey: '',
      pollIntervalMs: 30000,
      defaultDecisions: {},
      onUpdate: () => {},
      onError: () => {},
      ...config
    };

    this.startPolling();
  }

  public async isEnabled(flagKey: string, context: Context): Promise<boolean> {
    const flag = this.flags[flagKey];
    
    if (!flag) {
      // Return default decision if available
      return this.config.defaultDecisions[flagKey] ?? false;
    }

    return this.evaluateFlag(flag, context);
  }

  public getFlag(flagKey: string): FlagData | null {
    return this.flags[flagKey] || null;
  }

  public getAllFlags(): Record<string, FlagData> {
    return { ...this.flags };
  }

  public stop(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = undefined;
    }
    this.isPolling = false;
  }

  private startPolling(): void {
    if (this.isPolling) return;
    
    this.isPolling = true;
    
    // Initial load
    this.loadAllFlags().catch(error => {
      this.config.onError(error);
    });

    // Set up polling
    this.pollTimer = setInterval(async () => {
      try {
        await this.loadAllFlags();
      } catch (error) {
        this.config.onError(error as Error);
      }
    }, this.config.pollIntervalMs);
  }

  private async loadAllFlags(): Promise<void> {
    try {
      const response = await fetch(`${this.config.baseUrl}/flags`, {
        headers: this.getHeaders()
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const flagsList = await response.json() as Array<{
        key: string;
        description: string;
        rolloutPercent: number;
        updatedAt: string;
      }>;

      // Load individual flags with ETags
      await Promise.all(
        flagsList.map(flagInfo => this.loadFlag(flagInfo.key))
      );

    } catch (error) {
      throw new Error(`Failed to load flags: ${error}`);
    }
  }

  private async loadFlag(flagKey: string): Promise<void> {
    try {
      const headers = this.getHeaders();
      
      // Add If-None-Match header if we have an ETag
      if (this.etags[flagKey]) {
        headers['If-None-Match'] = `"${this.etags[flagKey]}"`;
      }

      const response = await fetch(`${this.config.baseUrl}/flags/${flagKey}`, {
        headers
      });

      if (response.status === 304) {
        // Not modified, keep current flag data
        return;
      }

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const flagData = await response.json() as FlagData;
      const etag = response.headers.get('etag')?.replace(/"/g, '');

      if (etag) {
        this.etags[flagKey] = etag;
        flagData.etag = etag;
      }

      const oldFlag = this.flags[flagKey];
      this.flags[flagKey] = flagData;

      // Trigger update callback if flag changed
      if (!oldFlag || JSON.stringify(oldFlag) !== JSON.stringify(flagData)) {
        this.config.onUpdate(this.getAllFlags());
      }

    } catch (error) {
      // On error, keep existing flag data and use defaults
      if (!this.flags[flagKey] && this.config.defaultDecisions[flagKey] !== undefined) {
        this.flags[flagKey] = {
          key: flagKey,
          description: 'Default flag (API unavailable)',
          rolloutPercent: this.config.defaultDecisions[flagKey] ? 100 : 0,
          rules: {}
        };
      }
      throw error;
    }
  }

  private evaluateFlag(flag: FlagData, context: Context): boolean {
    // Simple percentage-based evaluation using userId hash
    const hash = this.hashUserId(context.userId);
    const userPercent = hash % 100;
    
    // Basic rollout percentage check
    if (userPercent >= flag.rolloutPercent) {
      return false;
    }

    // TODO: Implement rules evaluation
    // For now, just use rollout percentage
    return true;
  }

  private hashUserId(userId: string): number {
    const hash = createHash('sha256').update(userId).digest('hex');
    return parseInt(hash.substring(0, 8), 16) % 100;
  }

  private getHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'User-Agent': 'EdgeControl-Node-SDK/1.0.0'
    };

    if (this.config.apiKey) {
      headers['Authorization'] = `Bearer ${this.config.apiKey}`;
    }

    return headers;
  }
}

// Factory function for easier initialization
export function init(config: EdgeControlConfig): EdgeControlClient {
  return new EdgeControlClient(config);
}
