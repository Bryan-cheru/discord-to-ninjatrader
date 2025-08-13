import { NT8Connection } from './connection';
import { TradeSignal } from '../types';
import { logger } from '../utils/logger';
import { getNT8InstrumentName } from '../config/instruments';

export class NT8TradeExecutor {
  private connection: NT8Connection;

  constructor() {
    const host = process.env.NT8_HOST || 'localhost';
    const port = parseInt(process.env.NT8_PORT || '36973');
    this.connection = new NT8Connection(host, port);
  }

  public async initialize(): Promise<void> {
    try {
      await this.connection.connect();
      logger.info('NT8 Trade Executor initialized');
    } catch (error) {
      logger.error('Failed to initialize NT8 Trade Executor:', error);
      throw error;
    }
  }

  public async executeSignal(signal: TradeSignal, account: string): Promise<void> {
    // Ensure connection is active before executing
    if (!this.connection.isConnectionActive()) {
      logger.warn('NT8 connection not active, attempting to reconnect...');
      try {
        await this.connection.connect();
      } catch (error) {
        throw new Error(`Failed to reconnect to NT8: ${error}`);
      }
    }

    try {
      // Create simple command format that matches the NT8 strategy
      // Use empty account string to use NT8's active account
      const command = this.createSimpleCommand(signal, '');
      logger.info(`🔄 Sending command to NT8: "${command}" (using active account)`);
      await this.connection.sendCommand(command);

      logger.info(`✅ Command sent successfully: ${signal.action} ${signal.quantity || ''} ${signal.symbol || ''}`);
    } catch (error) {
      logger.error('Failed to execute trade signal:', error);
      throw error;
    }
  }

  private createSimpleCommand(signal: TradeSignal, account: string): string {
    // Create new format commands that NT8 strategy expects
    // Examples: "BUY 2 MES STOP LIMIT @ 4950", "SELL 1 NQ", "CLOSE 2 MES", "MOVE SL TO 4950"
    
    let command = '';
    
    switch (signal.action) {
      case 'BUY':
        if (signal.orderType === 'STOP_LIMIT' && signal.price) {
          command = `BUY ${signal.quantity} ${signal.symbol} STOP LIMIT @ ${signal.price}`;
        } else {
          command = `BUY ${signal.quantity} ${signal.symbol}`;
        }
        break;
        
      case 'SELL':
        if (signal.orderType === 'STOP_LIMIT' && signal.price) {
          command = `SELL ${signal.quantity} ${signal.symbol} STOP LIMIT @ ${signal.price}`;
        } else {
          command = `SELL ${signal.quantity} ${signal.symbol}`;
        }
        break;
        
      case 'CLOSE':
        if (signal.symbol && signal.symbol !== 'ALL') {
          command = `CLOSE ${signal.quantity} ${signal.symbol}`;
        } else {
          command = 'CLOSE POSITION';
        }
        break;
        
      case 'CLOSE_POSITION':
        command = 'CLOSE POSITION';
        break;
        
      case 'MOVE_SL':
        command = `MOVE SL TO ${signal.stopLoss}`;
        break;
        
      case 'MOVE_TP':
        command = `MOVE TP TO ${signal.takeProfit}`;
        break;
        
      default:
        command = `${signal.action} ${signal.symbol || ''}`.trim();
    }
    
    // No account specification - NT8 will use the currently active account
    return command;
  }

  public async getAccountStatus(account: string): Promise<void> {
    logger.info(`ℹ️ Account status queries handled by NT8 strategy for account: ${account}`);
  }

  public async getPositions(account: string): Promise<void> {
    logger.info(`ℹ️ Position queries handled by NT8 strategy for account: ${account}`);
  }

  public async closeConnection(): Promise<void> {
    this.connection.disconnect();
    logger.info('NT8 Trade Executor connection closed');
  }
}
