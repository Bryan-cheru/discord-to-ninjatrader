#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class DiscordTradeCopier : Strategy
    {
        private TcpListener tcpListener;
        private System.Threading.Timer pollTimer;
        private bool isListening = false;
        private Dictionary<string, int> instrumentMap;
        private static int globalRestartCount = 0;
        private static DateTime globalLastRestartTime = DateTime.MinValue;
        private static bool globalListenerActive = false; // Track if any instance has an active listener
        private static DiscordTradeCopier activeInstance = null; // Singleton pattern
        
        // Instance-specific initialization flag
        private bool isInitialized = false;

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Tradable Instruments", Order = 0, GroupName = "Connection")]
        [Description("Comma-separated list of instruments to trade, e.g., NQ,ES,MNQ. The first instrument is the primary.")]
        public string TradableInstruments { get; set; }

        [NinjaScriptProperty]
        [Range(1024, 65535)]
        [Display(Name = "TCP Port", Order = 1, GroupName = "Connection")]
        public int TcpPort { get; set; } = 36971; // Match your NT8 configuration

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Default Quantity", Order = 2, GroupName = "Trading")]
        public int DefaultQuantity { get; set; } = 1;

        [NinjaScriptProperty]
        [Display(Name = "Enable Debug Logging", Order = 3, GroupName = "Debug")]
        public bool EnableDebugLogging { get; set; } = true;
        #endregion

        protected override void OnStateChange()
        {
            try
            {
                Print($"🔍 OnStateChange called with State: {State}");
                
                if (State == State.SetDefaults)
                {
                    // Enhanced restart protection with singleton check
                    if (activeInstance != null && globalListenerActive)
                    {
                        Print($"⚠️ Active strategy instance already running with listener. Blocking additional instance.");
                        return; // Prevent this instance from continuing
                    }
                    
                    // Always initialize instance variables to prevent null reference exceptions
                    instrumentMap = new Dictionary<string, int>();
                    isInitialized = true;
                    
                    Print($"🔧 Entering SetDefaults state at {DateTime.Now}");
                    
                    // Simplified restart protection - only track excessive restarts
                    if (DateTime.Now - globalLastRestartTime < TimeSpan.FromSeconds(60)) // Increased to 60 seconds
                    {
                        globalRestartCount++;
                        Print($"⚠️ Global restart #{globalRestartCount} within 60 seconds");
                        if (globalRestartCount > 15) // Increased threshold significantly  
                        {
                            Print($"❌ Too many global restarts ({globalRestartCount}) in 60 seconds. Strategy disabled.");
                            return;
                        }
                    }
                    else
                    {
                        Print($"✅ Global restart counter reset - last restart was over 60 seconds ago");
                        globalRestartCount = 0; // Reset counter after 60 seconds
                    }
                    globalLastRestartTime = DateTime.Now;

                    Print($"🔧 DiscordTradeCopier initializing (global restart #{globalRestartCount})");

                    Description = "Discord Trade Copier - Multi-Instrument";
                    Name = "DiscordTradeCopier";

                    // Default instruments - include common futures for testing
                    TradableInstruments = "NQ,ES,MNQ,MES,YM,RTY";

                    try
                    {
                        Print($"🔧 Setting strategy properties...");
                        
                        // Strategy settings with error handling
                        Calculate = Calculate.OnBarClose; // Changed from OnEachTick to reduce processing
                        EntriesPerDirection = 100;
                        EntryHandling = EntryHandling.AllEntries;
                        IsExitOnSessionCloseStrategy = false;
                        IsFillLimitOnTouch = false;
                        MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                        OrderFillResolution = OrderFillResolution.Standard;
                        Slippage = 0;
                        StartBehavior = StartBehavior.ImmediatelySubmit; 
                        TimeInForce = TimeInForce.Gtc;
                        TraceOrders = false;
                        RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                        StopTargetHandling = StopTargetHandling.PerEntryExecution;
                        BarsRequiredToTrade = 1; // Increased from 0 to help stability

                        Print($"✅ SetDefaults completed successfully");
                    }
                    catch (Exception setupEx)
                    {
                        Print($"❌ CRITICAL ERROR in SetDefaults setup: {setupEx.Message}");
                        Print($"❌ SetDefaults error stack trace: {setupEx.StackTrace}");
                        // Don't throw - let it continue
                    }
                }
                else if (State == State.Configure)
                {
                    try
                    {
                        Print($"⚙️ DiscordTradeCopier configuring - START");
                        
                        // Safety check - ensure instrumentMap is initialized
                        if (instrumentMap == null)
                        {
                            Print($"⚠️ instrumentMap was null in Configure - reinitializing");
                            instrumentMap = new Dictionary<string, int>();
                        }
                        
                        // Validate we have an instrument before proceeding
                        if (Instrument == null || Instrument.MasterInstrument == null)
                        {
                            Print($"❌ CRITICAL: Instrument is null in Configure state!");
                            throw new Exception("Instrument is null - cannot configure strategy");
                        }

                        Print($"📊 Primary instrument available: {Instrument.MasterInstrument.Name}");

                        // Add the primary instrument to the map
                        instrumentMap[Instrument.MasterInstrument.Name.ToUpper()] = 0;
                        Print($"📈 Primary instrument: {Instrument.MasterInstrument.Name} at index 0");

                        // Add additional data series for other instruments (simplified)
                        if (!string.IsNullOrEmpty(TradableInstruments))
                        {
                            string[] symbols = TradableInstruments.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            int addedCount = 0;
                            
                            Print($"📊 Attempting to configure {symbols.Length} total instruments: {string.Join(", ", symbols)}");
                            
                            foreach (string symbol in symbols)
                            {
                                string upperSymbol = symbol.Trim().ToUpper();
                                if (!instrumentMap.ContainsKey(upperSymbol) && upperSymbol != Instrument.MasterInstrument.Name.ToUpper())
                                {
                                    try
                                    {
                                        Print($"📈 Attempting to add data series for: {upperSymbol}");
                                        // Try to add data series with error handling
                                        AddDataSeries(upperSymbol, BarsPeriodType.Minute, 1);
                                        addedCount++;
                                        Print($"✅ Successfully added data series for: {upperSymbol}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Print($"⚠️ Failed to add data series for {upperSymbol}: {ex.Message}");
                                        Print($"⚠️ This symbol may not be available in your data feed or account");
                                        // Continue with other symbols instead of failing completely
                                    }
                                }
                                else if (upperSymbol == Instrument.MasterInstrument.Name.ToUpper())
                                {
                                    Print($"📈 {upperSymbol} is the primary instrument (already loaded)");
                                }
                            }
                            
                            Print($"📊 Added {addedCount} additional data series successfully");
                            Print($"📊 Total symbols to be mapped: {symbols.Length} configured");
                        }
                        
                        Print($"✅ Configure state completed successfully");
                    }
                    catch (Exception configEx)
                    {
                        Print($"❌ CRITICAL ERROR in Configure: {configEx.Message}");
                        Print($"❌ Configure Stack trace: {configEx.StackTrace}");
                        
                        // Don't throw exception - try to continue
                        Print($"⚠️ Continuing with primary instrument only due to Configure error");
                    }
                }
                else if (State == State.DataLoaded)
                {
                    try
                    {
                        LogMessage($"📊 DataLoaded phase started. BarsArray.Length: {BarsArray.Length}", true);
                        
                        // Safety check - ensure instrumentMap is initialized
                        if (instrumentMap == null)
                        {
                            LogMessage($"⚠️ instrumentMap was null in DataLoaded - reinitializing", true);
                            instrumentMap = new Dictionary<string, int>();
                        }
                        
                        // Clear and rebuild instrument map to avoid duplicates
                        instrumentMap.Clear();
                        
                        // ENHANCED: Map ALL loaded instruments automatically (not just TradableInstruments)
                        LogMessage($"🔍 DYNAMIC INSTRUMENT DISCOVERY: Scanning all loaded data series...", true);
                        
                        for (int i = 0; i < BarsArray.Length; i++)
                        {
                            if (BarsArray[i] == null || BarsArray[i].Instrument == null)
                            {
                                LogMessage($"⚠️ Null instrument at index {i}", true);
                                continue;
                            }
                            
                            string symbolName = BarsArray[i].Instrument.MasterInstrument.Name.ToUpper();
                            instrumentMap[symbolName] = i; // Use assignment to avoid duplicates
                            LogMessage($"📈 AUTO-MAPPED: {symbolName} → index {i}", true);
                        }
                        
                        // Additional scanning: Try to find other common instruments that might be loaded
                        LogMessage($"🔍 EXTENDED SCAN: Looking for additional available instruments...", true);
                        try
                        {
                            // Common futures symbols to check
                            string[] commonSymbols = { "NQ", "ES", "YM", "RTY", "MNQ", "MES", "MYM", "M2K", 
                                                     "CL", "GC", "SI", "ZB", "ZN", "ZF", "ZT", "6E", "6J", "6B", "6A" };
                            
                            foreach (string symbol in commonSymbols)
                            {
                                if (!instrumentMap.ContainsKey(symbol))
                                {
                                    try
                                    {
                                        var instrument = Instrument.GetInstrument(symbol);
                                        if (instrument != null)
                                        {
                                            LogMessage($"🔍 Found available (but not loaded): {symbol}", true);
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore errors for symbols that don't exist
                                    }
                                }
                            }
                        }
                        catch (Exception scanEx)
                        {
                            LogMessage($"⚠️ Extended scan error (non-critical): {scanEx.Message}", true);
                        }
                        
                        LogMessage($"✅ DYNAMIC MAPPING COMPLETE: {instrumentMap.Count} instruments ready for trading", true);
                        LogMessage($"📊 Available symbols: {string.Join(", ", instrumentMap.Keys)}", true);
                        
                        // Validate we have at least one instrument
                        if (instrumentMap.Count == 0)
                        {
                            LogMessage($"❌ CRITICAL: No instruments mapped in DataLoaded!", true);
                            return;
                        }
                        
                        LogMessage($"🎯 Strategy now supports ANY symbol that's loaded in NinjaTrader!", true);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"❌ CRITICAL ERROR in DataLoaded: {ex.Message}", true);
                        LogMessage($"❌ DataLoaded Stack trace: {ex.StackTrace}", true);
                        return; // Don't proceed to Active state
                    }
                }
                else if (State == State.Active)
                {
                    try
                    {
                        Print($"🚀 DiscordTradeCopier entering Active state on account: {Account?.DisplayName ?? "Unknown"}");
                        Print($"📊 Available instruments: {instrumentMap?.Count ?? 0}");
                        Print($"📊 Current State: {State}");
                        Print($"📊 BarsArray Length: {BarsArray?.Length ?? 0}");
                        
                        // Additional validation before starting listener
                        if (Account == null)
                        {
                            Print($"❌ CRITICAL: Account is null! Cannot proceed to Active state.");
                            return;
                        }
                        
                        if (BarsArray == null || BarsArray.Length == 0)
                        {
                            Print($"❌ CRITICAL: BarsArray is null or empty! BarsArray length: {BarsArray?.Length ?? 0}");
                            return;
                        }
                        
                        if (instrumentMap == null)
                        {
                            Print($"❌ CRITICAL: instrumentMap is null!");
                            return;
                        }
                        
                        // Validate that we have instruments mapped
                        if (instrumentMap.Count == 0)
                        {
                            Print($"❌ CRITICAL: No instruments mapped! Check TradableInstruments parameter.");
                            Print($"❌ TradableInstruments setting: '{TradableInstruments}'");
                            return;
                        }
                        
                        // Check if account is connected (warning only, don't fail)
                        if (Account.Connection.Status != ConnectionStatus.Connected)
                        {
                            Print($"⚠️ WARNING: Account connection status is {Account.Connection.Status}");
                        }
                        
                        Print($"🔧 About to start TCP listener on port {TcpPort}...");
                        
                        // Start listener immediately instead of using timer
                        try
                        {
                            StartListener();
                            Print($"✅ DiscordTradeCopier fully started and ready!");
                        }
                        catch (Exception startEx)
                        {
                            Print($"❌ Error starting listener: {startEx.Message}");
                            Print($"❌ StartListener error details: {startEx.StackTrace}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Print($"❌ CRITICAL ERROR in State.Active: {ex.Message}");
                        Print($"❌ Active Stack trace: {ex.StackTrace}");
                        // Don't throw - let it continue but log the error
                    }
                }
                else if (State == State.Terminated)
                {
                    Print($"🛑 DiscordTradeCopier terminating - cleaning up resources");
                    
                    try
                    {
                        StopListener();
                        
                        // Clear singleton reference if this was the active instance
                        if (activeInstance == this)
                        {
                            activeInstance = null;
                            Print($"🔄 Active instance cleared for future restarts");
                        }
                        
                        // Clear instrument map
                        if (instrumentMap != null)
                        {
                            instrumentMap.Clear();
                        }
                        
                        Print($"✅ Strategy cleanup completed");
                    }
                    catch (Exception ex)
                    {
                        Print($"❌ Error during termination cleanup: {ex.Message}");
                    }
                }
                else if (State == State.Historical)
                {
                    Print($"📚 Strategy entered Historical state");
                }
                else if (State == State.Transition)
                {
                    Print($"🔄 Strategy in Transition state");
                }
                else if (State == State.Realtime)
                {
                    Print($"🕐 Strategy entered Realtime state");
                    
                    // Enhanced singleton check - only allow one active instance
                    if (activeInstance != null && activeInstance != this && globalListenerActive)
                    {
                        Print($"⚠️ Another strategy instance is already active. This instance will remain passive.");
                        return;
                    }
                    
                    // This might be the state we need to handle!
                    if (instrumentMap != null && instrumentMap.Count > 0 && !isListening)
                    {
                        Print($"🚀 Attempting to start listener from Realtime state...");
                        try
                        {
                            // Set this as the active instance before starting listener
                            activeInstance = this;
                            StartListener();
                            Print($"✅ DiscordTradeCopier started from Realtime state!");
                        }
                        catch (Exception ex)
                        {
                            Print($"❌ Error starting listener from Realtime: {ex.Message}");
                            activeInstance = null; // Reset if failed to start
                        }
                    }
                }
                else
                {
                    Print($"🔍 Unhandled state: {State}");
                }
            }
            catch (Exception ex)
            {
                // Safe error logging that won't cause additional failures
                try
                {
                    Print($"❌ FATAL ERROR in OnStateChange [{State}]: {ex.Message}");
                    Print($"❌ FATAL ERROR Stack Trace: {ex.StackTrace}");
                }
                catch
                {
                    // If even Print fails, try basic console output
                    System.Console.WriteLine($"CRITICAL ERROR in OnStateChange: {ex.Message}");
                }
                
                // Don't re-throw the exception as it causes NT8 to restart the strategy
                // Instead, let it gracefully fail and stay in a known state
            }
        }

        private void StartListener()
        {
            try
            {
                LogMessage($"🔧 StartListener called. Current listening status: {isListening}", true);
                LogMessage($"🔧 Current TCP Port: {TcpPort}", true);
                LogMessage($"🔧 Global listener active: {globalListenerActive}", true);
                
                // Prevent multiple instances from starting listeners simultaneously
                if (globalListenerActive)
                {
                    LogMessage($"⚠️ Another strategy instance already has an active listener. Skipping.", true);
                    return;
                }
                
                // Stop any existing listener first
                if (tcpListener != null)
                {
                    LogMessage($"🔄 Stopping existing TCP listener", true);
                    try
                    {
                        tcpListener.Stop();
                    }
                    catch (Exception stopEx)
                    {
                        LogMessage($"⚠️ Error stopping existing listener: {stopEx.Message}", true);
                    }
                    tcpListener = null;
                }

                // Validate port is available
                if (TcpPort < 1024 || TcpPort > 65535)
                {
                    LogMessage($"❌ Invalid port number: {TcpPort}. Must be between 1024-65535", true);
                    return;
                }

                LogMessage($"🔌 Creating TCP listener on port {TcpPort}", true);
                tcpListener = new TcpListener(IPAddress.Any, TcpPort);
                
                LogMessage($"🔌 Starting TCP listener...", true);
                tcpListener.Start();
                isListening = true;
                globalListenerActive = true; // Mark global state
                LogMessage($"✅ TCP listener started successfully on port {TcpPort}", true);

                // Create and start the polling timer
                if (pollTimer != null)
                {
                    LogMessage($"🔄 Stopping existing poll timer", true);
                    pollTimer.Dispose();
                    pollTimer = null;
                }
                
                LogMessage($"⏰ Creating poll timer with 100ms interval", true);
                pollTimer = new System.Threading.Timer(PollForCommands, null, 0, 100);
                LogMessage($"✅ Poll timer started successfully", true);

                LogMessage($"✅ Discord Trade Copier READY! Listening on port {TcpPort}", true);
                LogMessage($"📈 Trading Instruments: {string.Join(", ", instrumentMap.Keys)}", true);
                LogMessage($"💬 Market Orders: BUY/SELL # SYMBOL", true);
                LogMessage($"💬 Limit Orders: BUY/SELL # SYMBOL LIMIT @ PRICE", true);
                LogMessage($"💬 Stop Limit Orders: BUY/SELL # SYMBOL STOP LIMIT @ PRICE", true);
                LogMessage($"💬 Close Orders: CLOSE SYMBOL, CLOSE POSITION", true);
                LogMessage($"🏦 Active Account: {Account?.DisplayName ?? "Unknown"}", true);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ CRITICAL ERROR starting TCP listener on port {TcpPort}: {ex.Message}", true);
                LogMessage($"❌ StartListener Stack trace: {ex.StackTrace}", true);
                
                // Check if port is in use
                if (ex.Message.Contains("address already in use") || ex.Message.Contains("Only one usage"))
                {
                    LogMessage($"⚠️ Port {TcpPort} is already in use. Try a different port or restart NinjaTrader.", true);
                }
                else if (ex.Message.Contains("access denied") || ex.Message.Contains("permission"))
                {
                    LogMessage($"⚠️ Access denied for port {TcpPort}. Try running NinjaTrader as administrator.", true);
                }
                
                isListening = false;
                globalListenerActive = false; // Reset global state on failure
                
                // Don't retry automatically - let user fix the issue first
                LogMessage($"❌ TCP listener failed to start. Strategy will not process Discord commands.", true);
                throw; // Re-throw to make the error visible in NT8
            }
        }

        private void StopListener()
        {
            try
            {
                isListening = false;
                
                // Only reset global state if this is the active instance
                if (activeInstance == this)
                {
                    globalListenerActive = false; // Reset global state
                }
                
                pollTimer?.Dispose();
                pollTimer = null;

                if (tcpListener != null)
                {
                    tcpListener.Stop();
                    tcpListener = null;
                    LogMessage($"🔌 TCP listener stopped (Port {TcpPort} released)", true);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error stopping listener: {ex.Message}", true);
            }
        }

        private static int pollCount = 0; // Move to class level
        
        private void PollForCommands(object state)
        {
            if (!isListening || tcpListener == null)
                return;

            try
            {
                // Add periodic heartbeat to show the listener is active
                pollCount++;
                if (pollCount % 100 == 0) // Every 10 seconds (100ms * 100)
                {
                    LogMessage($"💓 TCP Listener heartbeat - Port {TcpPort} active, Poll #{pollCount}", true);
                }

                if (tcpListener.Pending())
                {
                    LogMessage("📞 Incoming Discord command...", true);

                    using (TcpClient client = tcpListener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            LogMessage($"📨 Command received: '{command}'", true);

                            ProcessDiscordCommand(command);

                            // Send response back to Discord bot
                            byte[] response = Encoding.UTF8.GetBytes("✅ Command received\n");
                            stream.Write(response, 0, response.Length);
                            LogMessage($"📤 Response sent back to Discord bot", true);
                        }
                        else
                        {
                            LogMessage($"⚠️ No data received from Discord bot", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error receiving command: {ex.Message}", true);
                LogMessage($"❌ PollForCommands stack trace: {ex.StackTrace}", true);
            }
        }

        private void ProcessDiscordCommand(string command)
        {
            try
            {
                LogMessage($"🎯 ProcessDiscordCommand started with: '{command}'", true);
                
                if (string.IsNullOrEmpty(command))
                {
                    LogMessage("❌ Empty command received", true);
                    return;
                }

                // Convert to uppercase and split by spaces, preserve original for parsing
                string upperCommand = command.ToUpper().Replace(",", "");
                string[] parts = upperCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                LogMessage($"🔍 Command parts: [{string.Join(", ", parts)}]", true);

                if (parts.Length == 0)
                {
                    LogMessage("❌ Invalid command format - no parts found", true);
                    return;
                }

                LogMessage($"🎯 Processing: {upperCommand} on account: {Account?.DisplayName ?? "Unknown"}", true);

                // Handle different command formats
                if (upperCommand.StartsWith("BUY") && upperCommand.Contains("STOP LIMIT @"))
                {
                    LogMessage($"🔀 Routing to HandleBuyStopLimitCommand", true);
                    HandleBuyStopLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("SELL") && upperCommand.Contains("STOP LIMIT @"))
                {
                    LogMessage($"🔀 Routing to HandleSellStopLimitCommand", true);
                    HandleSellStopLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("BUY") && upperCommand.Contains("LIMIT @"))
                {
                    LogMessage($"🔀 Routing to HandleBuyLimitCommand", true);
                    HandleBuyLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("SELL") && upperCommand.Contains("LIMIT @"))
                {
                    LogMessage($"🔀 Routing to HandleSellLimitCommand", true);
                    HandleSellLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("BUY"))
                {
                    LogMessage($"🔀 Routing to HandleBuyMarketCommand", true);
                    HandleBuyMarketCommand(parts);
                }
                else if (upperCommand.StartsWith("SELL"))
                {
                    LogMessage($"🔀 Routing to HandleSellMarketCommand", true);
                    HandleSellMarketCommand(parts);
                }
                else if (upperCommand.StartsWith("CLOSE POSITION"))
                {
                    LogMessage($"🔀 Routing to HandleClosePositionCommand", true);
                    HandleClosePositionCommand(parts);
                }
                else if (upperCommand.StartsWith("CLOSE"))
                {
                    LogMessage($"🔀 Routing to HandleCloseSymbolCommand", true);
                    HandleCloseSymbolCommand(parts);
                }
                else if (upperCommand.StartsWith("MOVE SL TO"))
                {
                    LogMessage($"🔀 Routing to HandleMoveSLCommand", true);
                    HandleMoveSLCommand(parts);
                }
                else if (upperCommand.StartsWith("MOVE TP TO"))
                {
                    LogMessage($"🔀 Routing to HandleMoveTPCommand", true);
                    HandleMoveTPCommand(parts);
                }
                else
                {
                    // Legacy commands might not work as expected with multi-instrument.
                    // It's better to guide users to the new format.
                    LogMessage($"🔀 Routing to HandleLegacyCommand for '{parts[0]}'", true);
                    LogMessage($"⚠️ Legacy command '{parts[0]}' used. Please use new format for multi-instrument trading.", true);
                    HandleLegacyCommand(parts);
                }
                
                LogMessage($"✅ ProcessDiscordCommand completed for: '{command}'", true);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error processing command '{command}': {ex.Message}", true);
                LogMessage($"❌ ProcessDiscordCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private int GetInstrumentIndex(string symbol)
        {
            try
            {
                string upperSymbol = symbol.ToUpper();
                LogMessage($"🔍 GetInstrumentIndex - Looking for symbol: '{upperSymbol}'", true);
                LogMessage($"🔍 Currently mapped instruments: [{string.Join(", ", instrumentMap.Keys)}]", true);
                LogMessage($"🔍 Total instruments mapped: {instrumentMap.Count}", true);
                
                // First check if we already have this symbol mapped
                if (instrumentMap.ContainsKey(upperSymbol))
                {
                    int index = instrumentMap[upperSymbol];
                    LogMessage($"✅ Found {upperSymbol} at existing index {index}", true);
                    return index;
                }

                // Try to find the symbol in loaded BarsArray (dynamic detection)
                LogMessage($"🔍 Symbol not in map, searching BarsArray for: {upperSymbol}", true);
                for (int i = 0; i < BarsArray.Length; i++)
                {
                    if (BarsArray[i] != null && BarsArray[i].Instrument != null)
                    {
                        string barSymbol = BarsArray[i].Instrument.MasterInstrument.Name.ToUpper();
                        LogMessage($"🔍 Checking BarsArray[{i}]: {barSymbol}", true);
                        
                        if (barSymbol == upperSymbol)
                        {
                            // Add to our map for future use
                            instrumentMap[upperSymbol] = i;
                            LogMessage($"✅ DYNAMIC DISCOVERY: Added {upperSymbol} at index {i}", true);
                            return i;
                        }
                    }
                }

                // Try to find symbol using NinjaTrader's Instrument.GetInstrument method
                LogMessage($"🔍 Attempting to find instrument using GetInstrument: {upperSymbol}", true);
                try
                {
                    var instrument = Instrument.GetInstrument(upperSymbol);
                    if (instrument != null)
                    {
                        LogMessage($"✅ Found instrument via GetInstrument: {instrument.MasterInstrument.Name}", true);
                        LogMessage($"⚠️ However, this symbol is not loaded as a data series in the strategy", true);
                        LogMessage($"💡 To trade {upperSymbol}, add it to a chart or strategy first", true);
                        return -2; // Special code for "exists but not loaded"
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"🔍 GetInstrument failed for {upperSymbol}: {ex.Message}", true);
                }

                LogMessage($"❌ Instrument '{symbol}' not found in any loaded data series.", true);
                LogMessage($"💡 Available symbols: {string.Join(", ", instrumentMap.Keys)}", true);
                LogMessage($"💡 To trade {upperSymbol}:", true);
                LogMessage($"   1. Add {upperSymbol} to a chart in NinjaTrader", true);
                LogMessage($"   2. Or add it to TradableInstruments parameter: {upperSymbol}", true);
                LogMessage($"   3. Restart the strategy", true);
                return -1; // Indicates instrument not found
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in GetInstrumentIndex: {ex.Message}", true);
                return -1;
            }
        }

        private void HandleBuyCommand(string[] parts)
        {
            try
            {
                // Legacy Format: BUY [SYMBOL] [QUANTITY] [PRICE]
                if (parts.Length < 2)
                {
                    LogMessage("❌ Invalid legacy BUY format. Use: BUY SYMBOL [QTY] [PRICE]");
                    return;
                }
                string symbol = parts[1];
                int barsIndex = GetInstrumentIndex(symbol);
                if (barsIndex == -1) return;

                int quantity = DefaultQuantity;
                double price = 0;

                // Parse quantity if provided
                if (parts.Length > 2 && int.TryParse(parts[2], out int qty))
                {
                    quantity = qty;
                }

                // Parse price if provided (for limit orders)
                if (parts.Length > 3 && double.TryParse(parts[3], out double prc))
                {
                    price = prc;
                }

                if (price > 0)
                {
                    LogMessage($"🟢 LIMIT BUY: {quantity} {symbol} @ {price}");
                    EnterLongLimit(barsIndex, true, quantity, price, $"DiscordBuyLimit_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"🟢 MARKET BUY: {quantity} {symbol}");
                    EnterLong(barsIndex, quantity, $"DiscordBuy_{symbol}_{DateTime.Now.Ticks}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in legacy BUY command: {ex.Message}");
            }
        }

        private void HandleSellCommand(string[] parts)
        {
            try
            {
                // Legacy Format: SELL [SYMBOL] [QUANTITY] [PRICE]
                if (parts.Length < 2)
                {
                    LogMessage("❌ Invalid legacy SELL format. Use: SELL SYMBOL [QTY] [PRICE]");
                    return;
                }
                string symbol = parts[1];
                int barsIndex = GetInstrumentIndex(symbol);
                if (barsIndex == -1) return;

                int quantity = DefaultQuantity;
                double price = 0;

                if (parts.Length > 2 && int.TryParse(parts[2], out int qty))
                {
                    quantity = qty;
                }

                if (parts.Length > 3 && double.TryParse(parts[3], out double prc))
                {
                    price = prc;
                }

                if (price > 0)
                {
                    LogMessage($"🔴 LIMIT SELL: {quantity} {symbol} @ {price}");
                    EnterShortLimit(barsIndex, true, quantity, price, $"DiscordSellLimit_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"🔴 MARKET SELL: {quantity} {symbol}");
                    EnterShort(barsIndex, quantity, $"DiscordSell_{symbol}_{DateTime.Now.Ticks}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in legacy SELL command: {ex.Message}");
            }
        }

        private void HandleCloseCommand(string[] parts)
        {
            try
            {
                // Legacy Format: CLOSE [SYMBOL] or CLOSE ALL
                if (parts.Length > 1)
                {
                    string target = parts[1].ToUpper();
                    if (target == "ALL")
                    {
                        LogMessage($"❌ CLOSING ALL POSITIONS for all known instruments");
                        foreach (var entry in instrumentMap)
                        {
                            ExitLong(entry.Value, "Closing All", "");
                            ExitShort(entry.Value, "Closing All", "");
                        }
                    }
                    else
                    {
                        int barsIndex = GetInstrumentIndex(target);
                        if (barsIndex != -1)
                        {
                            LogMessage($"❌ CLOSING ALL POSITIONS for {target}");
                            ExitLong(barsIndex, "Closing Symbol", "");
                            ExitShort(barsIndex, "Closing Symbol", "");
                        }
                    }
                }
                else
                {
                    LogMessage($"❌ Invalid legacy CLOSE format. Use: CLOSE [SYMBOL] or CLOSE ALL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in legacy CLOSE command: {ex.Message}");
            }
        }

        private void HandleStopLossCommand(string[] parts)
        {
            LogMessage($"⚠️ Legacy SL command is not supported in multi-instrument mode. Use 'MOVE SL TO ...' for a specific position.");
        }

        private void HandleTakeProfitCommand(string[] parts)
        {
            LogMessage($"⚠️ Legacy TP command is not supported in multi-instrument mode. Use 'MOVE TP TO ...' for a specific position.");
        }

        private void HandleCancelCommand()
        {
            try
            {
                LogMessage($"🚫 CANCELING ALL WORKING ORDERS");

                // Cancel all orders manually by iterating through Account.Orders
                List<Order> ordersToCancel = new List<Order>();
                
                // Create a copy of orders to avoid collection modification during iteration
                foreach (Order order in Account.Orders)
                {
                    if (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
                    {
                        ordersToCancel.Add(order);
                    }
                }
                
                // Cancel the orders
                foreach (Order order in ordersToCancel)
                {
                    CancelOrder(order);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in CANCEL command: {ex.Message}");
            }
        }

        // New format command handlers
        private void HandleBuyStopLimitCommand(string[] parts)
        {
            try
            {
                // Format: BUY # SYMBOL STOP LIMIT @ ####
                if (parts.Length >= 6 && int.TryParse(parts[1], out int quantity) && double.TryParse(parts[5], out double price))
                {
                    string symbol = parts[2];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    // Note: Stop-Limit orders require a stop price and a limit price.
                    // This example assumes the limit price is slightly higher than the stop price.
                    // You may need to adjust the logic for how the limit price is determined.
                    double limitPrice = price + (BarsArray[barsIndex].Instrument.MasterInstrument.TickSize * 20); // Example: 20 ticks above stop
                    LogMessage($"🟢 STOP LIMIT BUY: {quantity} {symbol} @ {price} (Limit: {limitPrice})");
                    EnterLongStopLimit(barsIndex, true, quantity, limitPrice, price, $"DiscordBuyStopLimit_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"❌ Invalid BUY STOP LIMIT format. Use: BUY # SYMBOL STOP LIMIT @ ####");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in BUY STOP LIMIT command: {ex.Message}");
            }
        }

        private void HandleSellStopLimitCommand(string[] parts)
        {
            try
            {
                // Format: SELL # SYMBOL STOP LIMIT @ ####
                if (parts.Length >= 6 && int.TryParse(parts[1], out int quantity) && double.TryParse(parts[5], out double price))
                {
                    string symbol = parts[2];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    // Note: Stop-Limit orders require a stop price and a limit price.
                    // This example assumes the limit price is slightly lower than the stop price.
                    double limitPrice = price - (BarsArray[barsIndex].Instrument.MasterInstrument.TickSize * 20); // Example: 20 ticks below stop
                    LogMessage($"🔴 STOP LIMIT SELL: {quantity} {symbol} @ {price} (Limit: {limitPrice})");
                    EnterShortStopLimit(barsIndex, true, quantity, limitPrice, price, $"DiscordSellStopLimit_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"❌ Invalid SELL STOP LIMIT format. Use: SELL # SYMBOL STOP LIMIT @ ####");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in SELL STOP LIMIT command: {ex.Message}");
            }
        }

        private void HandleBuyLimitCommand(string[] parts)
        {
            try
            {
                LogMessage($"🟢 HandleBuyLimitCommand started with parts: [{string.Join(", ", parts)}]", true);
                
                // Format: BUY # SYMBOL LIMIT @ ####
                if (parts.Length >= 5 && int.TryParse(parts[1], out int quantity) && double.TryParse(parts[4], out double limitPrice))
                {
                    string symbol = parts[2];
                    LogMessage($"🔍 Parsed - Quantity: {quantity}, Symbol: {symbol}, Limit Price: {limitPrice}", true);
                    
                    int barsIndex = GetInstrumentIndex(symbol);
                    LogMessage($"🔍 Instrument index for {symbol}: {barsIndex}", true);
                    
                    if (barsIndex == -1) 
                    {
                        LogMessage($"❌ Instrument {symbol} not found or not loaded, aborting trade", true);
                        return;
                    }
                    else if (barsIndex == -2)
                    {
                        LogMessage($"❌ Instrument {symbol} exists but not loaded as data series, aborting trade", true);
                        LogMessage($"💡 Add {symbol} to a chart or include it in TradableInstruments parameter", true);
                        return;
                    }

                    LogMessage($"🟢 LIMIT BUY: {quantity} {symbol} @ {limitPrice}", true);
                    LogMessage($"🔧 Calling EnterLongLimit with barsIndex: {barsIndex}, quantity: {quantity}, limitPrice: {limitPrice}", true);
                    
                    // Execute the limit order
                    EnterLongLimit(barsIndex, true, quantity, limitPrice, $"DiscordBuyLimit_{symbol}_{DateTime.Now.Ticks}");
                    
                    LogMessage($"✅ EnterLongLimit call completed for {quantity} {symbol} @ {limitPrice}", true);
                }
                else
                {
                    LogMessage($"❌ Invalid BUY LIMIT format. Parts.Length: {parts.Length}. Use: BUY # SYMBOL LIMIT @ ####", true);
                    LogMessage($"💡 Example: BUY 1 NQ LIMIT @ 15000", true);
                    if (parts.Length >= 2)
                    {
                        LogMessage($"❌ Failed to parse quantity '{parts[1]}' as integer", true);
                    }
                    if (parts.Length >= 5)
                    {
                        LogMessage($"❌ Failed to parse limit price '{parts[4]}' as double", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in BUY LIMIT command: {ex.Message}", true);
                LogMessage($"❌ HandleBuyLimitCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private void HandleSellLimitCommand(string[] parts)
        {
            try
            {
                LogMessage($"🔴 HandleSellLimitCommand started with parts: [{string.Join(", ", parts)}]", true);
                
                // Format: SELL # SYMBOL LIMIT @ ####
                if (parts.Length >= 5 && int.TryParse(parts[1], out int quantity) && double.TryParse(parts[4], out double limitPrice))
                {
                    string symbol = parts[2];
                    LogMessage($"🔍 Parsed - Quantity: {quantity}, Symbol: {symbol}, Limit Price: {limitPrice}", true);
                    
                    int barsIndex = GetInstrumentIndex(symbol);
                    LogMessage($"🔍 Instrument index for {symbol}: {barsIndex}", true);
                    
                    if (barsIndex == -1) 
                    {
                        LogMessage($"❌ Instrument {symbol} not found or not loaded, aborting trade", true);
                        return;
                    }
                    else if (barsIndex == -2)
                    {
                        LogMessage($"❌ Instrument {symbol} exists but not loaded as data series, aborting trade", true);
                        LogMessage($"💡 Add {symbol} to a chart or include it in TradableInstruments parameter", true);
                        return;
                    }

                    LogMessage($"🔴 LIMIT SELL: {quantity} {symbol} @ {limitPrice}", true);
                    LogMessage($"🔧 Calling EnterShortLimit with barsIndex: {barsIndex}, quantity: {quantity}, limitPrice: {limitPrice}", true);
                    
                    // Execute the limit order
                    EnterShortLimit(barsIndex, true, quantity, limitPrice, $"DiscordSellLimit_{symbol}_{DateTime.Now.Ticks}");
                    
                    LogMessage($"✅ EnterShortLimit call completed for {quantity} {symbol} @ {limitPrice}", true);
                }
                else
                {
                    LogMessage($"❌ Invalid SELL LIMIT format. Parts.Length: {parts.Length}. Use: SELL # SYMBOL LIMIT @ ####", true);
                    LogMessage($"💡 Example: SELL 1 NQ LIMIT @ 15000", true);
                    if (parts.Length >= 2)
                    {
                        LogMessage($"❌ Failed to parse quantity '{parts[1]}' as integer", true);
                    }
                    if (parts.Length >= 5)
                    {
                        LogMessage($"❌ Failed to parse limit price '{parts[4]}' as double", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in SELL LIMIT command: {ex.Message}", true);
                LogMessage($"❌ HandleSellLimitCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private void HandleBuyMarketCommand(string[] parts)
        {
            try
            {
                LogMessage($"🟢 HandleBuyMarketCommand started with parts: [{string.Join(", ", parts)}]", true);
                
                // Format: BUY # SYMBOL
                if (parts.Length >= 3 && int.TryParse(parts[1], out int quantity))
                {
                    string symbol = parts[2];
                    LogMessage($"🔍 Parsed - Quantity: {quantity}, Symbol: {symbol}", true);
                    
                    int barsIndex = GetInstrumentIndex(symbol);
                    LogMessage($"🔍 Instrument index for {symbol}: {barsIndex}", true);
                    
                    if (barsIndex == -1) 
                    {
                        LogMessage($"❌ Instrument {symbol} not found or not loaded, aborting trade", true);
                        return;
                    }
                    else if (barsIndex == -2)
                    {
                        LogMessage($"❌ Instrument {symbol} exists but not loaded as data series, aborting trade", true);
                        LogMessage($"💡 Add {symbol} to a chart or include it in TradableInstruments parameter", true);
                        return;
                    }

                    LogMessage($"🟢 MARKET BUY: {quantity} {symbol}", true);
                    LogMessage($"🔧 Calling EnterLong with barsIndex: {barsIndex}, quantity: {quantity}", true);
                    
                    // Execute the trade
                    EnterLong(barsIndex, quantity, $"DiscordBuyMarket_{symbol}_{DateTime.Now.Ticks}");
                    
                    LogMessage($"✅ EnterLong call completed for {quantity} {symbol}", true);
                }
                else
                {
                    LogMessage($"❌ Invalid BUY format. Parts.Length: {parts.Length}. Use: BUY # SYMBOL", true);
                    if (parts.Length >= 2)
                    {
                        LogMessage($"❌ Failed to parse quantity '{parts[1]}' as integer", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in BUY command: {ex.Message}", true);
                LogMessage($"❌ HandleBuyMarketCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private void HandleSellMarketCommand(string[] parts)
        {
            try
            {
                LogMessage($"🔴 HandleSellMarketCommand started with parts: [{string.Join(", ", parts)}]", true);
                
                // Format: SELL # SYMBOL
                if (parts.Length >= 3 && int.TryParse(parts[1], out int quantity))
                {
                    string symbol = parts[2];
                    LogMessage($"🔍 Parsed - Quantity: {quantity}, Symbol: {symbol}", true);
                    
                    int barsIndex = GetInstrumentIndex(symbol);
                    LogMessage($"🔍 Instrument index for {symbol}: {barsIndex}", true);
                    
                    if (barsIndex == -1) 
                    {
                        LogMessage($"❌ Instrument {symbol} not found or not loaded, aborting trade", true);
                        return;
                    }
                    else if (barsIndex == -2)
                    {
                        LogMessage($"❌ Instrument {symbol} exists but not loaded as data series, aborting trade", true);
                        LogMessage($"💡 Add {symbol} to a chart or include it in TradableInstruments parameter", true);
                        return;
                    }

                    LogMessage($"🔴 MARKET SELL: {quantity} {symbol}", true);
                    LogMessage($"🔧 Calling EnterShort with barsIndex: {barsIndex}, quantity: {quantity}", true);
                    
                    // Execute the trade
                    EnterShort(barsIndex, quantity, $"DiscordSellMarket_{symbol}_{DateTime.Now.Ticks}");
                    
                    LogMessage($"✅ EnterShort call completed for {quantity} {symbol}", true);
                }
                else
                {
                    LogMessage($"❌ Invalid SELL format. Parts.Length: {parts.Length}. Use: SELL # SYMBOL", true);
                    if (parts.Length >= 2)
                    {
                        LogMessage($"❌ Failed to parse quantity '{parts[1]}' as integer", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in SELL command: {ex.Message}", true);
                LogMessage($"❌ HandleSellMarketCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private void HandleCloseSymbolCommand(string[] parts)
        {
            try
            {
                LogMessage($"❌ HandleCloseSymbolCommand started with parts: [{string.Join(", ", parts)}]", true);
                
                // Format: CLOSE # SYMBOL or CLOSE SYMBOL
                if (parts.Length >= 2)
                {
                    string symbol;
                    int quantity = 0;

                    // Check if quantity is provided
                    if (int.TryParse(parts[1], out int qty))
                    {
                        // Format: CLOSE # SYMBOL
                        if (parts.Length < 3)
                        {
                            LogMessage($"❌ Invalid CLOSE format. Use: CLOSE # SYMBOL or CLOSE SYMBOL", true);
                            return;
                        }
                        quantity = qty;
                        symbol = parts[2];
                    }
                    else
                    {
                        // Format: CLOSE SYMBOL
                        symbol = parts[1];
                    }

                    LogMessage($"🔍 Parsed - Symbol: {symbol}, Quantity: {quantity}", true);
                    
                    int barsIndex = GetInstrumentIndex(symbol);
                    LogMessage($"🔍 Instrument index for {symbol}: {barsIndex}", true);
                    
                    if (barsIndex == -1) 
                    {
                        LogMessage($"❌ Instrument {symbol} not found or not loaded, aborting close", true);
                        return;
                    }
                    else if (barsIndex == -2)
                    {
                        LogMessage($"❌ Instrument {symbol} exists but not loaded as data series, aborting close", true);
                        LogMessage($"💡 Add {symbol} to a chart or include it in TradableInstruments parameter", true);
                        return;
                    }

                    if (quantity > 0)
                    {
                        LogMessage($"❌ CLOSING: {quantity} {symbol}", true);
                        ExitLong(barsIndex, quantity, $"Closing {quantity}", "");
                        ExitShort(barsIndex, quantity, $"Closing {quantity}", "");
                    }
                    else
                    {
                        LogMessage($"❌ CLOSING ALL for {symbol}", true);
                        ExitLong(barsIndex, "Closing All", "");
                        ExitShort(barsIndex, "Closing All", "");
                    }
                    
                    LogMessage($"✅ Close command completed for {symbol}", true);
                }
                else
                {
                    LogMessage($"❌ Invalid CLOSE format. Use: CLOSE # SYMBOL or CLOSE SYMBOL", true);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in CLOSE command: {ex.Message}", true);
                LogMessage($"❌ HandleCloseSymbolCommand stack trace: {ex.StackTrace}", true);
            }
        }

        private void HandleClosePositionCommand(string[] parts)
        {
            try
            {
                // Format: CLOSE POSITION [SYMBOL]
                if (parts.Length > 2)
                {
                    string symbol = parts[2];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex != -1)
                    {
                        LogMessage($"❌ CLOSING POSITION for {symbol}");
                        ExitLong(barsIndex, "Closing Position", "");
                        ExitShort(barsIndex, "Closing Position", "");
                    }
                }
                else
                {
                    // Close all positions for all instruments
                    LogMessage($"❌ CLOSING ALL POSITIONS for all instruments");
                    foreach (var entry in instrumentMap)
                    {
                        ExitLong(entry.Value, "Closing All", "");
                        ExitShort(entry.Value, "Closing All", "");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in CLOSE POSITION command: {ex.Message}");
            }
        }

        private void HandleMoveSLCommand(string[] parts)
        {
            try
            {
                // Format: MOVE SL TO #### FOR SYMBOL
                if (parts.Length >= 6 && parts[4] == "FOR" && double.TryParse(parts[3], out double stopPrice))
                {
                    string symbol = parts[5];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    LogMessage($"🛑 MOVE STOP LOSS TO: {stopPrice} for {symbol}");
                    SetStopLoss(symbol, CalculationMode.Price, stopPrice, false);
                }
                else
                {
                    LogMessage($"❌ Invalid MOVE SL format. Use: MOVE SL TO #### FOR SYMBOL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in MOVE SL command: {ex.Message}");
            }
        }

        private void HandleMoveTPCommand(string[] parts)
        {
            try
            {
                // Format: MOVE TP TO #### FOR SYMBOL
                if (parts.Length >= 6 && parts[4] == "FOR" && double.TryParse(parts[3], out double targetPrice))
                {
                    string symbol = parts[5];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    LogMessage($"🎯 MOVE TAKE PROFIT TO: {targetPrice} for {symbol}");
                    SetProfitTarget(symbol, CalculationMode.Price, targetPrice, false);
                }
                else
                {
                    LogMessage($"❌ Invalid MOVE TP format. Use: MOVE TP TO #### FOR SYMBOL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in MOVE TP command: {ex.Message}");
            }
        }

        private void HandleLegacyCommand(string[] parts)
        {
            // Handle old format commands for backward compatibility
            string action = parts[0];

            switch (action)
            {
                case "BUY":
                case "LONG":
                    HandleBuyCommand(parts);
                    break;

                case "SELL":
                case "SHORT":
                    HandleSellCommand(parts);
                    break;

                case "CLOSE":
                case "EXIT":
                    HandleCloseCommand(parts);
                    break;

                case "SL":
                case "STOP":
                case "STOPLOSS":
                    HandleStopLossCommand(parts);
                    break;

                case "TP":
                case "TARGET":
                case "TAKEPROFIT":
                    HandleTakeProfitCommand(parts);
                    break;

                case "CANCEL":
                    HandleCancelCommand();
                    break;

                default:
                    LogMessage($"❓ Unknown command: {action}");
                    break;
            }
        }

        private void LogMessage(string message, bool forceLog = false)
        {
            try
            {
                if (EnableDebugLogging || forceLog)
                {
                    // Log in all states except SetDefaults (to avoid early state issues)
                    if (State != State.SetDefaults)
                    {
                        Print($"{DateTime.Now:HH:mm:ss} - {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback - use basic Print if LogMessage fails
                try
                {
                    Print($"LogMessage ERROR: {ex.Message} - Original: {message}");
                }
                catch
                {
                    // If even Print fails, just ignore to prevent cascade failures
                }
            }
        }

        protected override void OnBarUpdate()
        {
            // Safety checks to prevent crashes
            if (State != State.Realtime && State != State.Historical) return;
            if (CurrentBars == null || CurrentBars.Length == 0) return;
            if (CurrentBars[0] < 1) return;
            if (BarsInProgress < 0 || BarsInProgress >= BarsArray.Length) return;
            
            // Additional safety check for instrumentMap
            if (instrumentMap == null) return;

            try
            {
                // Reduced heartbeat frequency to prevent excessive logging
                if (BarsInProgress == 0 && CurrentBar % 2000 == 0 && CurrentBar > 0) // Changed from 500 to 2000
                {
                    LogMessage($"💓 Heartbeat - Bar {CurrentBar}, Port: {(isListening ? "Active" : "Inactive")}, Instruments: {instrumentMap.Count}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in OnBarUpdate: {ex.Message}");
                // Don't rethrow - just log and continue
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            // Log position updates
            if (position.Instrument != null)
            {
                LogMessage($"📊 Position [{position.Instrument.MasterInstrument.Name}]: {marketPosition} {Math.Abs(quantity)} @ {averagePrice:F2}", true);
            }
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            // Log all order updates to track what's happening
            if (order != null)
            {
                LogMessage($"🔔 Order Update - Signal: {order.Name}, State: {orderState}, Qty: {quantity}, Filled: {filled}, Price: {averageFillPrice:F2}, Error: {error}", true);
                
                if (error != ErrorCode.NoError)
                {
                    LogMessage($"❌ Order Error - {error}: {nativeError}", true);
                }
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Log executions
            if (execution != null && execution.Order != null)
            {
                LogMessage($"✅ Execution - Signal: {execution.Order.Name}, Qty: {quantity}, Price: {price:F2}, Position: {marketPosition}", true);
            }
        }
    }
}