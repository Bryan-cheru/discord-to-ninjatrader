import * as net from 'net';
import { logger } from '../utils/logger';
import { TradeSignal } from '../types';

export class NT8Connection {
  private socket: net.Socket | null = null;
  private host: string;
  private port: number;
  private isConnected: boolean = false;
  private reconnectAttempts: number = 0;
  private maxReconnectAttempts: number;
  private reconnectInterval: number;

  constructor(host: string = 'localhost', port: number = 36973, maxReconnectAttempts: number = 10, reconnectInterval: number = 5000) {
    this.host = host;
    this.port = port;
    this.maxReconnectAttempts = maxReconnectAttempts;
    this.reconnectInterval = reconnectInterval;
  }

  public async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      try {
        logger.info(`Connecting to NT8 at ${this.host}:${this.port}`);
        
        this.socket = new net.Socket();

        this.socket.on('connect', () => {
          logger.info('TCP connection established to NT8');
          this.isConnected = true;
          this.reconnectAttempts = 0;
          resolve();
        });

        this.socket.on('data', (data: Buffer) => {
          this.handleMessage(data.toString());
        });

        this.socket.on('close', () => {
          logger.warn('NT8 connection closed');
          this.isConnected = false;
          this.attemptReconnect();
        });

        this.socket.on('error', (error: Error) => {
          logger.error('NT8 connection error:', error);
          this.isConnected = false;
          if (this.reconnectAttempts === 0) {
            reject(error);
          }
        });

        this.socket.connect(this.port, this.host);

      } catch (error) {
        reject(error);
      }
    });
  }

  public disconnect(): void {
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
      this.isConnected = false;
      logger.info('Disconnected from NT8');
    }
  }

  public sendCommand(command: string): Promise<void> {
    return new Promise((resolve, reject) => {
      if (!this.isConnected || !this.socket) {
        reject(new Error('Not connected to NT8'));
        return;
      }

      try {
        this.socket.write(command + '\r\n');
        logger.info(`📤 Sent command to NT8: ${command}`);
        resolve();
      } catch (error) {
        reject(error);
      }
    });
  }

  private handleMessage(message: string): void {
    const cleanMessage = message.trim();
    logger.info(`📥 Received from NT8: ${cleanMessage}`);
    
    // Parse NT8 ATI responses for errors or confirmations
    if (cleanMessage.includes('ERROR') || cleanMessage.includes('REJECTED') || cleanMessage.includes('INVALID')) {
      logger.error(`❌ NT8 Error/Rejection: ${cleanMessage}`);
    } else if (cleanMessage.includes('ORDERCONFIRM')) {
      logger.info(`✅ Order Confirmed: ${cleanMessage}`);
    } else if (cleanMessage.includes('ORDERFILLED')) {
      logger.info(`✅ Order Filled: ${cleanMessage}`);
    } else if (cleanMessage.includes('ORDERWORKING')) {
      logger.info(`🔄 Order Working: ${cleanMessage}`);
    } else if (cleanMessage.includes('ORDERCANCELLED')) {
      logger.warn(`⚠️ Order Cancelled: ${cleanMessage}`);
    } else if (cleanMessage.includes('POSITION')) {
      logger.info(`📊 Position Update: ${cleanMessage}`);
    } else {
      logger.debug(`📊 NT8 Response: ${cleanMessage}`);
    }
  }

  private attemptReconnect(): void {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      logger.error('Max reconnection attempts reached');
      return;
    }

    this.reconnectAttempts++;
    logger.info(`Attempting to reconnect to NT8 (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);

    setTimeout(() => {
      this.connect().catch(error => {
        logger.error('Reconnection attempt failed:', error);
      });
    }, this.reconnectInterval);
  }

  public isConnectionActive(): boolean {
    return this.isConnected;
  }
}
