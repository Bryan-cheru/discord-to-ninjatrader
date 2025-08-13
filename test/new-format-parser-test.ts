import { SignalParser } from '../src/parser/signalParser';
import { logger } from '../src/utils/logger';

// Test the new format parser
const parser = new SignalParser();

console.log('=== Discord-NT8 Trade Copier - NEW FORMAT Parser Test ===');

const testMessages = [
  'BUY 2 MES STOP LIMIT @ 4950',
  'SELL 1 NQ STOP LIMIT @ 16800',
  'BUY 3 MES',
  'SELL 1 NQ',
  'CLOSE 2 MES',
  'CLOSE POSITION',
  'MOVE SL TO 4900',
  
  // Legacy format tests
  'BUY NQ',
  'SELL ES 2',
  'CLOSE ALL',
  'SL 4950',
  'TP 5050',
  'CANCEL'
];

testMessages.forEach((message, index) => {
  console.log(`Test ${index + 1}: "${message}"`);
  
  try {
    const signal = parser.parseMessage(message, 'test-channel');
    
    if (signal) {
      console.log('✅ PARSED:', {
        action: signal.action,
        symbol: signal.symbol,
        quantity: signal.quantity,
        orderType: signal.orderType,
        price: signal.price || 'N/A',
        stopLoss: signal.stopLoss || 'N/A',
        takeProfit: signal.takeProfit || 'N/A'
      });
      
      // Show what command would be sent to NT8
      const isValidSignal = parser.isValidSignal(signal);
      console.log(`📤 Command would be generated: ${isValidSignal ? '✅' : '❌'}`);
      
      if (isValidSignal) {
        console.log('🎯 VALID SIGNAL FOR NT8');
      }
    } else {
      console.log('❌ NOT PARSED - No matching pattern');
    }
  } catch (error) {
    console.log('❌ ERROR:', error);
  }
  
  console.log('---');
});

console.log('=== NEW FORMAT Test Complete ===');
console.log('New format commands should now work with your NT8 strategy!');
