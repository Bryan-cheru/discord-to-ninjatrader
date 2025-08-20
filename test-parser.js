// Test the signal parser with the new limit patterns
const { SignalParser } = require('./dist/parser/signalParser');

function testParsing() {
    console.log('🧪 Testing Signal Parser Patterns\n');
    
    const parser = new SignalParser();
    
    const testMessages = [
        'BUY 1 MNQ LIMIT @ 6397',     // Your failing command
        'SELL 1 MNQ LIMIT @ 6400',    // Sell limit test
        'BUY 2 MES LIMIT @ 4950',     // Different symbol
        'BUY 1 MNQ',                  // Market order (should work)
        'BUY 1 MNQ STOP LIMIT @ 6400', // Stop limit (should work)
        'INVALID COMMAND'             // Should fail
    ];
    
    console.log('Testing signal parsing...\n');
    
    testMessages.forEach((message, i) => {
        console.log(`📤 Test ${i + 1}: "${message}"`);
        
        try {
            const signal = parser.parseMessage(message, 'test-channel');
            
            if (signal) {
                console.log('✅ Parsed successfully:');
                console.log(`   Action: ${signal.action}`);
                console.log(`   Symbol: ${signal.symbol}`);
                console.log(`   Quantity: ${signal.quantity}`);
                console.log(`   Order Type: ${signal.orderType}`);
                if (signal.price) console.log(`   Price: ${signal.price}`);
            } else {
                console.log('❌ No pattern matched');
            }
        } catch (error) {
            console.log(`❌ Parse error: ${error.message}`);
        }
        
        console.log('');
    });
    
    console.log('🎯 Parser testing completed!');
}

testParsing();
