# Multiple Chart Support - Setup Guide

## Overview
The updated DiscordTradeCopier strategy now supports multiple chart instances running simultaneously! This means you can:

✅ **Open multiple charts** with different instruments  
✅ **Run the strategy on each chart** independently  
✅ **Receive Discord commands** on any chart that has the matching instrument  
✅ **Automatic command routing** to the correct chart instance  

## How It Works

### Single TCP Listener
- Only **one instance** runs the TCP listener (connects to Discord bot)
- Other instances remain **passive** but ready to receive routed commands
- If the active instance is closed, another instance **automatically takes over**

### Intelligent Command Routing  
When a Discord command comes in:
1. 🔍 **Extract instrument** from command (e.g., "BUY 1 MES" → MES)
2. 📊 **Check current instance** - can it handle MES?
3. 🔄 **Route to appropriate chart** if another instance has MES loaded
4. ✅ **Execute trade** on the correct chart/account

### Instance Management
- ⚡ **Up to 10 instances** can run simultaneously  
- 🏷️ **Unique instance IDs** prevent conflicts
- 🔄 **Automatic cleanup** when charts are closed
- 📋 **Instance tracking** for efficient routing

## Setup Instructions

### 1. Multiple Chart Method (Recommended)
```
1. Open first chart (e.g., MES 1-minute)
2. Apply DiscordTradeCopier strategy → Set instruments: "MES,NQ,ES"
3. Open second chart (e.g., NQ 1-minute)  
4. Apply DiscordTradeCopier strategy → Set instruments: "NQ,YM,RTY"
5. Open third chart (e.g., YM 1-minute)
6. Apply DiscordTradeCopier strategy → Set instruments: "YM,CL,GC"
```

### 2. Configuration Per Chart
Each chart can specify its **preferred instruments** in the strategy parameters:
- **Tradable Instruments**: `"MES,NQ,ES"` (comma-separated)
- **TCP Port**: `36971` (same for all instances)
- **Default Quantity**: Adjust per chart needs

### 3. Discord Bot Connection
- ✅ **Same Discord bot** connects to all instances
- ✅ **Same port** (36971) for all instances  
- ✅ **Automatic routing** based on instrument

## Example Scenarios

### Scenario 1: Futures Trading
```
Chart 1: MES chart → Strategy with "MES,ES,SPY"
Chart 2: NQ chart → Strategy with "NQ,QQQ,TQQQ"  
Chart 3: YM chart → Strategy with "YM,DIA,UDOW"

Discord Command: "BUY 1 MES" → Routes to Chart 1
Discord Command: "BUY 2 NQ" → Routes to Chart 2  
Discord Command: "SELL 1 YM" → Routes to Chart 3
```

### Scenario 2: Multi-Timeframe
```
Chart 1: ES 1-minute → Strategy with "ES,MES"
Chart 2: ES 5-minute → Strategy with "ES,MES"
Chart 3: ES 15-minute → Strategy with "ES,MES"

All charts can handle ES/MES commands
First available chart processes the command
```

### Scenario 3: Different Accounts
```
Chart 1: Sim Account → Strategy with "MES,NQ"
Chart 2: Live Account → Strategy with "ES,YM" 
Chart 3: Paper Account → Strategy with "CL,GC"

Commands route to appropriate account based on instrument
```

## Benefits

### ✅ Advantages
- **Faster execution** - dedicated chart per instrument
- **Better organization** - separate charts for different strategies
- **Account separation** - different charts can use different accounts
- **Timeframe flexibility** - multiple timeframes per instrument
- **Automatic failover** - if one chart closes, others continue working

### ⚠️ Considerations  
- **Memory usage** - multiple charts use more RAM
- **Chart management** - more charts to organize
- **Symbol conflicts** - same symbol on multiple charts = first wins

## Troubleshooting

### "No active listener" Message
```
✅ Expected: Only ONE chart should show "Active TCP listener"
✅ Others show: "TCP listener already active on another instance"
```

### Command Not Executing
```
1. Check Output Window on ALL charts
2. Look for routing messages: "Routing MES command to instance..."
3. Verify instrument is loaded: Check "Auto-mapped: MES → index 0"
4. Confirm account connection on target chart
```

### Multiple TCP Errors
```
❌ Problem: "Address already in use"
✅ Solution: Only one strategy should handle TCP (automatic)
✅ Check: Look for "Making this instance the TCP handler"
```

## Testing Multiple Charts

Run the test script to verify routing:
```bash
node test-multiple-charts.js
```

This will send commands for different instruments and show you the routing in action!

## Best Practices

1. **Start with one chart** - get basic functionality working first
2. **Add charts gradually** - verify each new instance connects properly  
3. **Monitor Output Window** - watch for routing and execution messages
4. **Use different instruments** - avoid symbol conflicts between charts
5. **Check account settings** - ensure each chart uses intended account

The system is now much more flexible and can handle complex multi-instrument trading setups!
