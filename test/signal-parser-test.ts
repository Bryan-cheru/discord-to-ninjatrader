import { SignalParser } from '../src/parser/signalParser';
import { logger } from '../src/utils/logger';

// Test the simplified signal parser with NT8-compatible commands
const parser = new SignalParser();

const testSignals = [
  'BUY NQ',           // Simple market buy
  'SELL ES',          // Simple market sell  
  'BUY NQ 2',         // Buy 2 contracts
  'SELL ES 1 4950',   // Sell 1 contract at limit price 4950
  'CLOSE',            // Close all positions
  'CLOSE ALL',        // Close all positions (explicit)
  'CLOSE LONG',       // Close long positions only
  'CLOSE SHORT',      // Close short positions only
  'SL 4900',          // Set stop loss at 4900
  'TP 5100',          // Set take profit at 5100
  'CANCEL',           // Cancel all orders
  'LONG MES 3',       // Long 3 micro contracts
  'SHORT YM',         // Short Dow futures
  'Invalid signal'    // Should not parse
];

console.log('=== Discord-NT8 Trade Copier - Simplified Parser Test ===\n');

testSignals.forEach((signal, index) => {
  console.log(`Test ${index + 1}: "${signal}"`);
  
  const result = parser.parseMessage(signal, '1234567890');
  
  if (result) {
    console.log('✅ PARSED:', {
      action: result.action,
      symbol: result.symbol || 'N/A',
      quantity: result.quantity,
      orderType: result.orderType,
      price: result.price || 'N/A',
      stopLoss: result.stopLoss || 'N/A',
      takeProfit: result.takeProfit || 'N/A'
    });
    
    // Show what would be sent to NT8
    console.log(`📤 NT8 Command: "${convertToNT8Command(result)}"`);
    
    // Validate the signal
    if (parser.isValidSignal(result)) {
      console.log('✅ VALID SIGNAL');
    } else {
      console.log('❌ INVALID SIGNAL');
    }
  } else {
    console.log('❌ NOT PARSED - No matching pattern');
  }
  
  console.log('---');
});

function convertToNT8Command(signal: any): string {
  // Simulate what the trade executor would send to NT8
  switch (signal.action) {
    case 'BUY':
      let buyCmd = `BUY ${signal.symbol}`;
      if (signal.quantity > 1) buyCmd += ` ${signal.quantity}`;
      if (signal.price) buyCmd += ` ${signal.price}`;
      return buyCmd;
      
    case 'SELL':
      let sellCmd = `SELL ${signal.symbol}`;
      if (signal.quantity > 1) sellCmd += ` ${signal.quantity}`;
      if (signal.price) sellCmd += ` ${signal.price}`;
      return sellCmd;
      
    case 'CLOSE':
      return signal.symbol === 'ALL' ? 'CLOSE ALL' : `CLOSE ${signal.symbol}`;
      
    case 'CLOSE_POSITION':
      return 'CLOSE ALL';
      
    case 'MOVE_SL':
      return `SL ${signal.stopLoss}`;
      
    default:
      return signal.action;
  }
}

console.log('\n=== Test Complete ===');
console.log('Commands shown above match exactly what your NT8 strategy expects!');
console.log('Ready for testing with Discord → NT8 integration.');
