const net = require('net');

// Test 1: Check if NT8 port is listening
console.log('🔍 Testing NT8 TCP connection on port 36971...\n');

function testPortListening() {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        
        client.setTimeout(5000); // 5 second timeout
        
        client.connect(36971, 'localhost', () => {
            console.log('✅ Port 36971 is listening and accepting connections');
            client.destroy();
            resolve(true);
        });
        
        client.on('error', (err) => {
            console.log('❌ Port 36971 connection failed:', err.message);
            reject(err);
        });
        
        client.on('timeout', () => {
            console.log('❌ Connection timeout - port might not be listening');
            client.destroy();
            reject(new Error('Connection timeout'));
        });
    });
}

// Test 2: Send a test command to NT8
function sendTestCommand() {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        
        client.connect(36971, 'localhost', () => {
            console.log('📤 Sending test command: "BUY 1 NQ"');
            
            // Send the command
            client.write('BUY 1 NQ');
            
            // Wait for response
            client.on('data', (data) => {
                const response = data.toString().trim();
                console.log('📨 NT8 Response:', response);
                client.destroy();
                resolve(response);
            });
            
            // Timeout if no response in 10 seconds
            setTimeout(() => {
                console.log('⏰ No response from NT8 within 10 seconds');
                client.destroy();
                reject(new Error('No response timeout'));
            }, 10000);
        });
        
        client.on('error', (err) => {
            console.log('❌ Error sending command:', err.message);
            reject(err);
        });
    });
}

// Test 3: Monitor for heartbeat messages (just check if we can connect)
function monitorHeartbeat() {
    return new Promise((resolve) => {
        const client = new net.Socket();
        
        console.log('💓 Monitoring for heartbeat activity for 15 seconds...');
        
        client.connect(36971, 'localhost', () => {
            console.log('🔗 Connected - monitoring for any data from NT8...');
            
            client.on('data', (data) => {
                console.log('📊 Data received from NT8:', data.toString());
            });
            
            // Monitor for 15 seconds
            setTimeout(() => {
                console.log('⏰ Heartbeat monitoring complete');
                client.destroy();
                resolve();
            }, 15000);
        });
        
        client.on('error', (err) => {
            console.log('❌ Heartbeat monitoring error:', err.message);
            resolve();
        });
    });
}

// Run all tests
async function runTests() {
    console.log('🧪 Starting NT8 Connection Diagnostic Tests\n');
    console.log('=' .repeat(50));
    
    try {
        // Test 1
        console.log('\n📋 TEST 1: Port Listening Check');
        console.log('-'.repeat(30));
        await testPortListening();
        
        // Wait a moment
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        // Test 2
        console.log('\n📋 TEST 2: Send Test Command');
        console.log('-'.repeat(30));
        await sendTestCommand();
        
        // Wait a moment
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        // Test 3
        console.log('\n📋 TEST 3: Monitor Heartbeat');
        console.log('-'.repeat(30));
        await monitorHeartbeat();
        
    } catch (error) {
        console.log('\n❌ Test failed:', error.message);
    }
    
    console.log('\n' + '='.repeat(50));
    console.log('🏁 Diagnostic tests complete');
    console.log('\nNext steps:');
    console.log('1. Check NT8 Output window for any new log messages');
    console.log('2. Look for TCP Listener heartbeat messages every 10 seconds');
    console.log('3. If tests pass but Discord fails, the issue is with the Discord bot');
}

runTests();
