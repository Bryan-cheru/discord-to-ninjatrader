import { Client, GatewayIntentBits, Message } from 'discord.js';
import { logger } from '../utils/logger';
import { MessageHandler } from './messageHandler';

export class DiscordBot {
  private client: Client;
  private messageHandler: MessageHandler;

  constructor() {
    this.client = new Client({
      intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent
      ]
    });

    this.messageHandler = new MessageHandler();
    this.setupEventHandlers();
  }

  private setupEventHandlers(): void {
    this.client.once('ready', () => {
      logger.info(`Discord bot logged in as ${this.client.user?.tag}`);
      
      // Log server and channel information
      const guilds = this.client.guilds.cache;
      logger.info(`Bot is in ${guilds.size} server(s):`);
      guilds.forEach(guild => {
        logger.info(`- Server: ${guild.name} (ID: ${guild.id}) with ${guild.memberCount} members`);
        
        // Check if our watched channels exist in this server
        const propChannel = guild.channels.cache.get(process.env.PROP_CHANNEL_ID!);
        const liveChannel = guild.channels.cache.get(process.env.LIVE_CHANNEL_ID!);
        const otherChannel = guild.channels.cache.get(process.env.OTHER_CHANNEL_ID!);
        
        if (propChannel) {
          logger.info(`✅ Found Prop Channel: #${propChannel.name} (${propChannel.id})`);
        } else {
          logger.warn(`❌ Prop Channel (${process.env.PROP_CHANNEL_ID}) not found in ${guild.name}`);
        }
        
        if (liveChannel) {
          logger.info(`✅ Found Live Channel: #${liveChannel.name} (${liveChannel.id})`);
        } else {
          logger.warn(`❌ Live Channel (${process.env.LIVE_CHANNEL_ID}) not found in ${guild.name}`);
        }

        if (otherChannel) {
          logger.info(`✅ Found Other Channel: #${otherChannel.name} (${otherChannel.id})`);
        } else {
          logger.warn(`❌ Other Channel (${process.env.OTHER_CHANNEL_ID}) not found in ${guild.name}`);
        }
      });
    });

    this.client.on('messageCreate', (message: Message) => {
      this.messageHandler.handleMessage(message);
    });

    this.client.on('error', (error) => {
      logger.error('Discord client error:', error);
    });
  }

  public async start(token: string): Promise<void> {
    try {
      await this.client.login(token);
      logger.info('Discord bot started successfully');
    } catch (error) {
      logger.error('Failed to start Discord bot:', error);
      throw error;
    }
  }

  public async stop(): Promise<void> {
    try {
      await this.client.destroy();
      logger.info('Discord bot stopped');
    } catch (error) {
      logger.error('Error stopping Discord bot:', error);
    }
  }
}
