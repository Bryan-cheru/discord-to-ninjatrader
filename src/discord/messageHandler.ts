import { Message } from 'discord.js';
import { logger } from '../utils/logger';
import { SignalParser } from '../parser/signalParser';
import { NT8TradeExecutor } from '../nt8/tradeExecutor';
import { TradeSignal } from '../types';

export class MessageHandler {
  private signalParser: SignalParser;
  private tradeExecutor: NT8TradeExecutor;
  private watchedChannels: Set<string>;

  constructor(tradeExecutor: NT8TradeExecutor) {
    this.signalParser = new SignalParser();
    this.tradeExecutor = tradeExecutor; // Use the shared, initialized executor
    this.watchedChannels = new Set([
      process.env.PROP_CHANNEL_ID!,
      process.env.LIVE_CHANNEL_ID!,
      process.env.OTHER_CHANNEL_ID!
    ]);
  }

  public async handleMessage(message: Message): Promise<void> {
    // Log all messages for debugging
    logger.info(`Message received: "${message.content}" from channel ${message.channelId} by ${message.author.username}`);
    
    // Ignore bot messages and messages from unwatched channels
    if (message.author.bot) {
      logger.info('Ignoring bot message');
      return;
    }
    
    if (!this.watchedChannels.has(message.channelId)) {
      logger.info(`Ignoring message from unwatched channel ${message.channelId}`);
      return;
    }

    logger.info(`Processing message from watched channel ${message.channelId}: ${message.content}`);

    try {
      const tradeSignal = this.signalParser.parseMessage(message.content, message.channelId);
      
      if (tradeSignal) {
        logger.info('Trade signal parsed:', tradeSignal);
        await this.executeTradeSignal(tradeSignal, message.channelId);
      }
    } catch (error) {
      logger.error('Error handling message:', error);
    }
  }

  private async executeTradeSignal(signal: TradeSignal, channelId: string): Promise<void> {
    try {
      // Send command to NT8 without specifying account - it will use the active account
      await this.tradeExecutor.executeSignal(signal, '');
      logger.info(`Trade signal executed successfully - using active NT8 account`);
    } catch (error) {
      logger.error(`Failed to execute trade signal:`, error);
    }
  }
}
