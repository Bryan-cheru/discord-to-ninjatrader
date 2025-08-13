# Discord-NT8 Trade Copier Troubleshooting Guide

## Common Issues and Solutions

### 1. **Orders Not Being Executed**

**Symptoms:**
- Commands sent to NT8 but no orders appear
- Logs show "Trade executed successfully" but nothing happens

**Solutions:**

#### A. Check NT8 Strategy Setup
1. Load the `DiscordTradeCopier.cs` strategy in NT8
2. Apply it to a chart for the account you want to trade
3. **CRITICAL:** Make sure the account name in NT8 exactly matches your .env file:
   ```
   NT8_PROP_ACCOUNT=Sim101    <- Must match EXACTLY
   NT8_LIVE_ACCOUNT=DEMO44986 <- Must match EXACTLY
   ```

#### B. Verify Contract Months
- Update `src/config/instruments.ts` with current active contracts
- As of August 2025, December 2025 contracts (12-25) should be active
- Check NT8 Data tab to confirm available contracts

#### C. Enable NT8 Strategy Correctly
1. Right-click chart → Strategies → Add Strategy
2. Select "DiscordTradeCopier"
3. Set "Start behavior" to "Wait until flat"
4. **IMPORTANT:** Strategy must be enabled and running

### 2. **Connection Issues**

**Symptoms:**
- "NT8 connection not active, attempting to reconnect"
- Frequent reconnections in logs

**Solutions:**

#### A. Port Configuration
1. Ensure port 36973 is not blocked by firewall
2. No other applications using this port
3. NT8 and Discord bot running on same machine

#### B. NT8 ATI Not Responding
1. Check if DiscordTradeCopier strategy is loaded and enabled
2. Strategy must be applied to a chart and running
3. Check NT8 Output window for error messages

### 3. **Signal Parsing Issues**

**Symptoms:**
- "No trade signal pattern matched for message"
- Signals not being recognized

**Current Supported Patterns:**
```
BUY NQ                    <- Simple market order
BUY NQ @ 16800           <- Market order with price reference
LONG ES 4500 stop 4480 target 4520  <- Full entry with SL/TP
CLOSE NQ                 <- Close specific position
EXIT ALL                 <- Close all positions
```

### 4. **Account/Channel Mapping**

**Check your .env file:**
```
PROP_CHANNEL_ID=1401520030436556883  -> NT8_PROP_ACCOUNT=Sim101
LIVE_CHANNEL_ID=1401520132626321438  -> NT8_LIVE_ACCOUNT=DEMO44986
OTHER_CHANNEL_ID=1401678425533579284 -> NT8_OTHER_ACCOUNT=Sim101
```

### 5. **Discord Bot Issues**

**Symptoms:**
- Bot not receiving messages
- Channel IDs not found

**Solutions:**
1. Verify bot has proper permissions in Discord server
2. Bot needs "Read Messages" and "Read Message History" permissions
3. Check channel IDs are correct (right-click channel → Copy ID)

## Testing Procedure

### Step 1: Test Connection
```bash
npm run dev
```
Look for:
- ✅ Found Prop Channel
- ✅ Found Live Channel  
- ✅ Found Other Channel
- NT8 ATI connection verified

### Step 2: Test Signal Recognition
Send test message in Discord: `BUY NQ`
Should see:
- Signal matched pattern: simpleMarket
- Trade signal parsed
- Generated NT8 command

### Step 3: Verify NT8 Strategy
Check NT8 Output window for:
- "DiscordTradeCopier listening on port 36973"
- "Received command: PLACE ORDER;..."
- "Order submitted: BUY 1 NQ 12-25"

## Performance Optimization

### 1. **Instrument Configuration**
Update contract months quarterly in `src/config/instruments.ts`:
```typescript
'NQ': {
  nt8Name: 'NQ 03-26', // Update to March 2026 when rollover happens
}
```

### 2. **Order Management**
- Use ATM strategies in NT8 for automatic stop-loss/take-profit
- Configure position sizing in environment variables
- Set appropriate risk management parameters

### 3. **Logging Configuration**
```env
LOG_LEVEL=debug    # For troubleshooting
LOG_LEVEL=info     # For production
```

## NT8 Strategy Requirements

The C# strategy must be:
1. Compiled successfully in NT8
2. Applied to a chart with the correct account
3. Enabled and running ("State: Running" in Strategies tab)
4. Account names must match exactly

## Common Error Messages

| Error | Cause | Solution |
|-------|--------|----------|
| "Command not for this account" | Account name mismatch | Check .env file vs NT8 account name |
| "NT8 connection not active" | Strategy not running | Enable strategy in NT8 |
| "No trade signal pattern matched" | Unsupported message format | Use supported signal patterns |
| "Failed to execute trade signal" | NT8 connection issue | Restart NT8 strategy |

## Best Practices

1. **Start NT8 first**, then the Discord bot
2. **Test with small positions** initially
3. **Monitor both NT8 Output and bot logs** simultaneously
4. **Update contract months** before expiration
5. **Use simulation accounts** for testing
6. **Backup your configuration files** regularly

## Support

If issues persist:
1. Check NT8 Output window for errors
2. Enable debug logging: `LOG_LEVEL=debug`
3. Verify all permissions and configurations
4. Test with simple signals first (e.g., "BUY NQ")
