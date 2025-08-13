<<<<<<< HEAD
# discord-to-ninjatrader
=======
# Discord-NT8 Trade Copier

A local Discord-to-NinjaTrader 8 trade copier that listens to 3 Discord channels and routes trade signals to specific NT8 accounts with ATM (Advanced Trade Management) support.

## Features

- ✅ Listens to 3 private Discord channels (Prop, Live, Other Account)
- ✅ Routes trades to correct NT8 accounts based on channel
- ✅ Parses multiple trade signal formats including new command types
- ✅ Supports ATM strategies for advanced trade management
- ✅ Executes market, limit, and stop-limit orders
- ✅ Supports stop loss and take profit orders
- ✅ Position management commands (CLOSE, MOVE SL, CLOSE POSITION)
- ✅ Runs fully local (no cloud dependencies)
- ✅ Comprehensive logging system
- ✅ Automatic reconnection handling

## Supported Command Formats

### Traditional Formats:
- `BUY EURUSD @ 1.0850 SL: 1.0800 TP: 1.0900`
- `SELL GOLD 2050 STOP 2040 TARGET 2060`
- `Entry: BUY GBPUSD Price: 1.2500 SL: 1.2450 TP: 1.2550`

### New Command Formats:
- `BUY # MES STOP LIMIT` - Buy with stop-limit order and ATM
- `SELL # MES STOP LIMIT` - Sell with stop-limit order and ATM
- `CLOSE # MES` - Close specific symbol position
- `MOVE SL TO 4200` - Move stop loss to specified price
- `CLOSE POSITION` - Close all positions for the account
# Discord-NT8 Trade Copier
A local Discord-to-NinjaTrader 8 trade copier that listens to 3 Discord channels and routes trade signals to specific NT8 accounts with ATM (Advanced Trade Management) support.
- Node.js 18+ installed
- NinjaTrader 8 with ATI (Automated Trading Interface) enabled
- Discord bot token
   - Copy `.env.example` to `.env`
   - Fill in your Discord bot token and 3 channel IDs:
     - `PROP_CHANNEL_ID` - Channel for prop account trades
     - `LIVE_CHANNEL_ID` - Channel for live account trades  
     - `OTHER_CHANNEL_ID` - Channel for other account trades
   - Configure your 3 NT8 account names:
     - `NT8_PROP_ACCOUNT` - Your prop trading account
     - `NT8_LIVE_ACCOUNT` - Your live trading account
     - `NT8_OTHER_ACCOUNT` - Your other trading account
   - Adjust NT8 connection settings if needed

3. **Configure NinjaTrader 8:**
   - Enable ATI in NT8: Tools → Options → Automated Trading Interface
   - Set the port (default: 36973)
   - Enable the accounts you want to trade with

4. **Build the project:**
   ```bash
   npm run build
   ```

## Usage

### Development mode:
```bash
npm run dev
```

### Production mode:
```bash
npm start
```

## Configuration

### Environment Variables (.env)
```
DISCORD_BOT_TOKEN=your_discord_bot_token_here
CHANNEL_1_ID=your_first_channel_id_here  
CHANNEL_2_ID=your_second_channel_id_here
NT8_ACCOUNT_1=your_first_nt8_account_name
NT8_ACCOUNT_2=your_second_nt8_account_name
NT8_HOST=localhost
NT8_PORT=36973
LOG_LEVEL=info
LOG_TO_FILE=true
```

### Supported Signal Formats

The bot recognizes multiple trade signal formats:

1. **Standard Format:**
   ```
   BUY EURUSD @ 1.0850 SL: 1.0800 TP: 1.0900
   SELL GBPUSD @ 1.2500 SL: 1.2550 TP: 1.2450
   ```

2. **Alternative Format:**
   ```
   LONG GOLD 2050 Stop 2040 Target 2060
   SHORT CRUDE 75.50 Stop 76.00 Target 74.00
   ```

3. **Detailed Format:**
   ```
   Entry: BUY GBPUSD Price: 1.2500 SL: 1.2450 TP: 1.2550
   Entry: SELL EURUSD Price: 1.0850 SL: 1.0900 TP: 1.0800
   ```

## Channel Routing

- Messages from `CHANNEL_1_ID` → routed to `NT8_ACCOUNT_1`
- Messages from `CHANNEL_2_ID` → routed to `NT8_ACCOUNT_2`

## Logging

Logs are written to:
- Console (with colors)
- `logs/combined.log` (all logs)
- `logs/error.log` (errors only)
- `logs/exceptions.log` (uncaught exceptions)

## Project Structure

```
src/
├── app.ts                 # Main application entry point
├── discord/
│   ├── bot.ts            # Discord bot implementation
│   └── messageHandler.ts # Message processing logic
├── nt8/
│   ├── connection.ts     # NT8 WebSocket connection
│   └── tradeExecutor.ts  # Trade execution logic
├── parser/
│   └── signalParser.ts   # Trade signal parsing
├── utils/
│   └── logger.ts         # Logging configuration
└── types/
    └── index.ts          # TypeScript type definitions
```

## Troubleshooting

### Common Issues

1. **Discord bot not responding:**
   - Check bot token is correct
   - Ensure bot has proper permissions in channels
   - Verify channel IDs are correct

2. **NT8 connection failed:**
   - Ensure NT8 is running
   - Check ATI is enabled in NT8
   - Verify port number (default: 36973)
   - Check firewall settings

3. **Trades not executing:**
   - Verify NT8 account names are correct
   - Check account is connected and funded
   - Review logs for error messages

### Debug Mode

Set `LOG_LEVEL=debug` in `.env` for detailed logging.

## Security Notes

- Keep your `.env` file secure and never commit it
- The bot runs locally - no data is sent to external services
- Consider running in a secure network environment

## License

MIT License
>>>>>>> d89e646 (Initial commit: add all project files)
