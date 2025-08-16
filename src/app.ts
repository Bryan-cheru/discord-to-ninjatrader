import * as dotenv from 'dotenv';
import { DiscordBot } from './discord/bot';
import { NT8TradeExecutor } from './nt8/tradeExecutor';
import { logger } from './utils/logger';

// Load environment variables
dotenv.config();

class App {
  private discordBot: DiscordBot;
  private nt8Executor: NT8TradeExecutor;

  constructor() {
    this.nt8Executor = new NT8TradeExecutor();
    this.discordBot = new DiscordBot(this.nt8Executor); // Pass the shared executor
  }

  public async start(): Promise<void> {
    try {
      logger.info('Starting Discord-NT8 Trade Copier...');

      // Validate required environment variables
      this.validateEnvironment();

      // Initialize NT8 connection
      await this.nt8Executor.initialize();

      // Start Discord bot
      const discordToken = process.env.DISCORD_BOT_TOKEN!;
      await this.discordBot.start(discordToken);

      logger.info('Discord-NT8 Trade Copier started successfully!');

      // Setup graceful shutdown
      this.setupGracefulShutdown();

    } catch (error) {
      logger.error('Failed to start application:', error);
      process.exit(1);
    }
  }

  private validateEnvironment(): void {
    const requiredEnvVars = [
      'DISCORD_BOT_TOKEN',
      'PROP_CHANNEL_ID',
      'LIVE_CHANNEL_ID',
      'OTHER_CHANNEL_ID'
    ];

    const missingVars = requiredEnvVars.filter(varName => !process.env[varName]);

    if (missingVars.length > 0) {
      throw new Error(`Missing required environment variables: ${missingVars.join(', ')}`);
    }

    logger.info('Environment validation passed - using active NT8 account');
  }

  private setupGracefulShutdown(): void {
    const shutdown = async (signal: string) => {
      logger.info(`Received ${signal}. Shutting down gracefully...`);
      
      try {
        await this.discordBot.stop();
        await this.nt8Executor.closeConnection();
        logger.info('Application shut down successfully');
        process.exit(0);
      } catch (error) {
        logger.error('Error during shutdown:', error);
        process.exit(1);
      }
    };

    process.on('SIGINT', () => shutdown('SIGINT'));
    process.on('SIGTERM', () => shutdown('SIGTERM'));
  }
}

// Start the application
const app = new App();
app.start().catch(error => {
  logger.error('Unhandled error:', error);
  process.exit(1);
});
