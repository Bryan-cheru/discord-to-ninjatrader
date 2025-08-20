const net = require('net');

async function testMultipleChartSupport() {
    console.log('🧪 Testing Multiple Chart Support\n');
    
    try {
        // Test different instruments to see routing
        const testCommands = [
            'BUY 1 MES',           // Micro E-mini S&P 500
            'BUY 1 NQ',            // Nasdaq 100
            'BUY 1 ES',            // E-mini S&P 500
            'SELL 1 YM',           // Dow Jones
            'BUY 2 RTY LIMIT @ 2100',  // Russell 2000
            'CLOSE MES',           // Close MES position
        ];
        
        console.log('📋 Test Commands:');
        testCommands.forEach((cmd, i) => {
            console.log(`   ${i + 1}. ${cmd}`);
        });
        console.log('\n' + '='.repeat(50) + '\n');
        
        for (let i = 0; i < testCommands.length; i++) {
            const command = testCommands[i];
            console.log(`📤 Test ${i + 1}/6: ${command}`);
            
            try {
                const response = await sendCommand(command);
                console.log(`📥 Response: ${response}`);
                console.log('✅ Success\n');
                
                // Wait between commands
                await new Promise(resolve => setTimeout(resolve, 1000));
            } catch (error) {
                console.log(`❌ Error: ${error.message}\n`);
            }
        }
        
        console.log('🎯 Test completed! Check NinjaTrader Output Window to see:');
        console.log('   • Which instance handled each command');
        console.log('   • Command routing messages');
        console.log('   • Multiple chart instance logs');
        
    } catch (error) {
        console.error('❌ Test failed:', error.message);
    }
}

function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        let responseData = '';
        
        const timeout = setTimeout(() => {
            client.destroy();
            reject(new Error('Connection timeout'));
        }, 5000);
        
        client.connect(36971, 'localhost', () => {
            console.log('   🔌 Connected to NinjaTrader');
            client.write(command);
        });
        
        client.on('data', (data) => {
            responseData += data.toString();
            clearTimeout(timeout);
            client.destroy();
            resolve(responseData.trim());
        });
        
        client.on('error', (error) => {
            clearTimeout(timeout);
            reject(error);
        });
        
        client.on('close', () => {
            clearTimeout(timeout);
            if (!responseData) {
                reject(new Error('No response received'));
            }
        });
    });
}

testMultipleChartSupport();
