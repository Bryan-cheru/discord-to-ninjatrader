const { Client, GatewayIntentBits } = require('discord.js');
require('dotenv').config();

console.log('🔍 Testing Discord Bot Token...\n');

// Read token from .env file
const token = process.env.DISCORD_BOT_TOKEN;

if (!token || token === 'YOUR_NEW_TOKEN_HERE_PASTE_FROM_DISCORD_DEVELOPER_PORTAL') {
    console.log('❌ No token found in .env file or placeholder token detected');
    console.log('💡 Please add a valid DISCORD_BOT_TOKEN to your .env file');
    process.exit(1);
}

console.log(`🔑 Testing token: ${token.substring(0, 20)}...${token.substring(token.length - 10)}`);

// Create minimal Discord client
const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent
    ]
});

// Set up event handlers
client.once('ready', () => {
    console.log('✅ SUCCESS: Discord bot token is VALID!');
    console.log(`🤖 Bot logged in as: ${client.user.tag}`);
    console.log(`🆔 Bot ID: ${client.user.id}`);
    console.log(`🌐 Bot is in ${client.guilds.cache.size} server(s)`);
    
    client.guilds.cache.forEach(guild => {
        console.log(`   • ${guild.name} (${guild.id})`);
    });
    
    console.log('\n🎉 Token is working! Discord bot can connect successfully.');
    console.log('🚀 You can now run "npm start" to start the full application.');
    
    // Close the connection
    client.destroy();
    process.exit(0);
});

client.on('error', (error) => {
    console.log('❌ Discord client error:', error.message);
    process.exit(1);
});

// Attempt to login
console.log('🔄 Attempting to connect to Discord...');

client.login(token).catch(error => {
    console.log('❌ FAILED: Discord bot token is INVALID');
    console.log(`❌ Error: ${error.message}`);
    console.log(`❌ Error Code: ${error.code}`);
    
    if (error.code === 'TokenInvalid') {
        console.log('\n💡 How to fix this:');
        console.log('1. Go to: https://discord.com/developers/applications');
        console.log('2. Select your application or create a new one');
        console.log('3. Go to "Bot" section');
        console.log('4. Click "Reset Token" or "Copy Token"');
        console.log('5. Replace DISCORD_BOT_TOKEN in your .env file');
        console.log('6. Run this test again');
    }
    
    process.exit(1);
});

// Timeout after 10 seconds
setTimeout(() => {
    console.log('⏰ Connection timeout after 10 seconds');
    console.log('❌ Token appears to be invalid or network issue');
    client.destroy();
    process.exit(1);
}, 10000);
