# NinjaTrader 8 Strategy Troubleshooting Guide

## The strategy keeps terminating immediately? Here's how to fix it:

### **Most Common Causes & Solutions:**

### 1. **Data/Instrument Issues**
- **Problem**: NinjaTrader can't load data for one or more instruments
- **Solution**: 
  - Check that you have active data connections
  - Verify instruments exist (NQ, ES, MNQ, MES)
  - Try with just one instrument first: Set `TradableInstruments = "NQ"` in strategy parameters

### 2. **Account Issues**
- **Problem**: Account is not connected or has insufficient permissions
- **Solution**:
  - Ensure your account is connected (green in Connections tab)
  - Try with a Simulation account first
  - Check account has trading permissions

### 3. **Multiple Strategy Instances**
- **Problem**: Multiple instances trying to use the same port
- **Solution**:
  - Check if strategy is already running elsewhere
  - Use different port numbers for different instances
  - Remove old strategy instances from charts

### 4. **Insufficient Bars/Data**
- **Problem**: Not enough historical data available
- **Solution**:
  - Wait for market hours
  - Ensure data feed is connected
  - Try with different time frames

## **Step-by-Step Diagnosis:**

### **Step 1: Basic Setup**
1. Start with minimal configuration:
   ```
   TradableInstruments = "NQ"
   TcpPort = 36973
   EnableDebugLogging = true
   ```

2. Apply strategy to NQ chart only
3. Check NT8 Output window for detailed logs

### **Step 2: Check Logs**
Look for these specific error patterns in NT8 Output:
- `CRITICAL ERROR in DataLoaded` - Data loading issue
- `CRITICAL ERROR in State.Active` - Account/permission issue  
- `Failed to add data series` - Instrument not available
- `Account is null` - Account connection issue

### **Step 3: Progressive Testing**
1. **Test with 1 instrument**: `TradableInstruments = "NQ"`
2. **If that works, add more**: `TradableInstruments = "NQ,ES"`
3. **Continue until you find the problematic instrument**

### **Step 4: Port Issues**
If you see "address already in use":
1. Change port number: `TcpPort = 36974`
2. Or restart NinjaTrader completely
3. Check Windows Task Manager for lingering NT processes

## **Quick Fixes:**

### **Fix 1: Reset Strategy**
1. Remove strategy from all charts
2. Close NinjaTrader completely
3. Restart NinjaTrader
4. Recompile strategy (F5 in NinjaScript Editor)
5. Apply to single chart with minimal settings

### **Fix 2: Simplify Configuration**
```csharp
// In strategy parameters, use minimal setup:
TradableInstruments = "NQ"     // Just one instrument
TcpPort = 36973               // Default port
DefaultQuantity = 1           // Minimal quantity
EnableDebugLogging = true     // Enable logging
```

### **Fix 3: Check Account Setup**
1. Go to Tools → Account → Select your account
2. Ensure it shows "Connected" status
3. Verify trading permissions are enabled
4. Try with Sim101 account first

## **Expected Good Startup Logs:**
```
🔧 DiscordTradeCopier initializing (restart #0)
⚙️ DiscordTradeCopier configuring
📈 Primary instrument: NQ at index 0
📊 DataLoaded phase started. BarsArray.Length: 1
📈 Mapped instrument: NQ to index 0
✅ DataLoaded completed. 1 instruments mapped.
🚀 DiscordTradeCopier entering Active state on account: Sim101
✅ DiscordTradeCopier fully started and ready!
🔌 Creating TCP listener on port 36973
✅ Discord Trade Copier READY! Listening on port 36973
```

## **Red Flags (Bad Logs):**
```
❌ CRITICAL: No instruments mapped in DataLoaded!
❌ Account is null!
❌ BarsArray is null or empty!
❌ Too many restarts (3) in short time
```

## **Emergency Reset:**
If nothing works:
1. Close NinjaTrader completely
2. Delete strategy from all charts
3. Restart computer
4. Open NinjaTrader
5. Recompile strategy (F5)
6. Apply to fresh chart with simulation account
7. Use only "NQ" as tradable instrument

## **Contact Info:**
If issues persist after trying these steps, provide:
1. Full NT8 Output window log
2. Your strategy parameter settings
3. Account type (Sim/Live)
4. Instruments you're trying to trade
