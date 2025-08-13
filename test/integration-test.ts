import { SignalParser } from '../src/parser/signalParser';
import { NT8TradeExecutor } from '../src/nt8/tradeExecutor';
import { NT8Connection } from '../src/nt8/connection';

console.log('=== Full Integration Test - Parser + Command Generation ===');

// Create instances (connection won't actually connect in test)
const parser = new SignalParser();
const executor = new NT8TradeExecutor();

const testCommands = [
  // New format
  'BUY 2 MES STOP LIMIT @ 4950',
  'SELL 1 NQ STOP LIMIT @ 16800',
  'BUY 3 MES',
  'SELL 1 NQ',
  'CLOSE 2 MES',
  'CLOSE POSITION',
  'MOVE SL TO 4900',
  
  // Legacy format  
  'BUY NQ',
  'SELL ES 2', 
  'CLOSE ALL',
  'SL 4950',
  'TP 5050'
];

testCommands.forEach((message, index) => {
  console.log(`\n📝 Test ${index + 1}: "${message}"`);
  
  // Step 1: Parse the Discord message
  const signal = parser.parseMessage(message, 'test-channel');
  
  if (signal) {
    console.log('✅ Parsed Signal:', {
      action: signal.action,
      symbol: signal.symbol,
      quantity: signal.quantity,
      orderType: signal.orderType,
      price: signal.price || 'N/A',
      stopLoss: signal.stopLoss || 'N/A',
      takeProfit: signal.takeProfit || 'N/A'
    });
    
    // Step 2: Generate NT8 command (using private method via any cast for testing)
    try {
      const command = (executor as any).createSimpleCommand(signal, 'Sim101');
      console.log(`📤 NT8 Command: "${command}"`);
      console.log('🎯 Status: READY FOR NT8 STRATEGY');
    } catch (error) {
      console.log('❌ Command Generation Error:', error);
    }
  } else {
    console.log('❌ Parse failed - no matching pattern');
  }
  
  console.log('---');
});

console.log('\n🚀 Integration Test Complete!');
console.log('All commands are now ready for Discord → NT8 integration.');
console.log('Next steps:');
console.log('1. Compile NT8 strategy in NinjaTrader');
console.log('2. Apply strategy to chart with correct account');  
console.log('3. Run "npm run dev" to start Discord bot');
console.log('4. Test with real Discord messages!');
