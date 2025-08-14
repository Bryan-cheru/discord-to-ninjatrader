import * as dotenv from 'dotenv';
import { logger } from './src/utils/logger';
import { NT8TradeExecutor } from './src/nt8/tradeExecutor';
import { SignalParser } from './src/parser/signalParser';

// Load environment variables
dotenv.config();

class DiscordTestBot {
    private nt8Executor: NT8TradeExecutor;
    private signalParser: SignalParser;

    constructor() {
        this.nt8Executor = new NT8TradeExecutor();
        this.signalParser = new SignalParser();
    }

    async runTests() {
        console.log('🧪 Starting Discord Bot Component Tests\n');
        console.log('=' .repeat(50));

        try {
            // Test 1: Initialize NT8 connection
            console.log('\n📋 TEST 1: NT8 Connection Initialization');
            console.log('-'.repeat(40));
            await this.nt8Executor.initialize();
            console.log('✅ NT8 connection initialized');

            // Test 2: Parse signal
            console.log('\n📋 TEST 2: Signal Parsing');
            console.log('-'.repeat(40));
            const testMessage = 'BUY 1 NQ';
            const testChannelId = process.env.PROP_CHANNEL_ID || 'test-channel';
            console.log('📤 Testing message:', testMessage);
            console.log('📤 Testing channel:', testChannelId);
            
            const signal = this.signalParser.parseMessage(testMessage, testChannelId);
            if (signal) {
                console.log('✅ Signal parsed successfully:');
                console.log('   Action:', signal.action);
                console.log('   Quantity:', signal.quantity);
                console.log('   Symbol:', signal.symbol);
                console.log('   Channel:', signal.channelId);
            } else {
                console.log('❌ Signal parsing failed');
            }

            // Test 3: Execute trade
            console.log('\n📋 TEST 3: Trade Execution');
            console.log('-'.repeat(40));
            if (signal) {
                console.log('📤 Sending trade command to NT8...');
                const testAccount = 'Sim101'; // Default test account
                await this.nt8Executor.executeSignal(signal, testAccount);
                console.log('✅ Trade command sent (check NT8 for execution)');
            }

            // Test 4: Direct TCP command
            console.log('\n📋 TEST 4: Direct TCP Test');
            console.log('-'.repeat(40));
            console.log('📤 Sending raw command directly...');
            await this.testDirectTCP();

        } catch (error) {
            console.error('❌ Test failed:', error);
        }

        console.log('\n' + '='.repeat(50));
        console.log('🏁 Discord Bot tests complete');
    }

    private async testDirectTCP(): Promise<void> {
        return new Promise((resolve, reject) => {
            const net = require('net');
            const client = new net.Socket();

            client.connect(36971, 'localhost', () => {
                console.log('🔗 Direct TCP connection established');
                
                const command = 'BUY 1 NQ';
                console.log('📤 Sending command:', command);
                client.write(command);

                // Wait for response or timeout
                const timeout = setTimeout(() => {
                    console.log('⏰ No response within 5 seconds');
                    client.destroy();
                    resolve();
                }, 5000);

                client.on('data', (data: any) => {
                    clearTimeout(timeout);
                    console.log('📨 Response received:', data.toString());
                    client.destroy();
                    resolve();
                });
            });

            client.on('error', (err: any) => {
                console.log('❌ Direct TCP error:', err.message);
                reject(err);
            });
        });
    }

    async cleanup() {
        try {
            await this.nt8Executor.closeConnection();
            console.log('🧹 Cleanup complete');
        } catch (error) {
            console.error('❌ Cleanup error:', error);
        }
    }
}

// Run the tests
async function main() {
    const testBot = new DiscordTestBot();
    
    try {
        await testBot.runTests();
    } finally {
        await testBot.cleanup();
        process.exit(0);
    }
}

main().catch(error => {
    console.error('💥 Unhandled error:', error);
    process.exit(1);
});
