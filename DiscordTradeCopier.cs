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
        private System.Windows.Threading.DispatcherTimer pollTimer;
        private bool isListening = false;
        private Dictionary<string, int> instrumentMap = new Dictionary<string, int>();
        private int restartCount = 0;
        private DateTime lastRestartTime = DateTime.MinValue;

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Tradable Instruments", Order = 0, GroupName = "Connection")]
        [Description("Comma-separated list of instruments to trade, e.g., NQ,ES,MNQ. The first instrument is the primary.")]
        public string TradableInstruments { get; set; }

        [NinjaScriptProperty]
        [Range(1024, 65535)]
        [Display(Name = "TCP Port", Order = 1, GroupName = "Connection")]
        public int TcpPort { get; set; } = 36973;

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
                if (State == State.SetDefaults)
                {
                    // Check for excessive restarts with more aggressive protection
                    if (DateTime.Now - lastRestartTime < TimeSpan.FromMinutes(2)) // Increased from 1 minute
                    {
                        restartCount++;
                        if (restartCount > 3) // Reduced from 5 to be more conservative
                        {
                            LogMessage($"❌ Too many restarts ({restartCount}) in short time. Strategy disabled to prevent infinite loop.", true);
                            LogMessage($"❌ Please check logs for errors, restart NinjaTrader, and try again.", true);
                            return;
                        }
                    }
                    else
                    {
                        restartCount = 0; // Reset counter after 2 minutes
                    }
                    lastRestartTime = DateTime.Now;

                    Description = "Discord Trade Copier - Multi-Instrument";
                    Name = "DiscordTradeCopier";

                    // Default instruments
                    TradableInstruments = "NQ,ES,MNQ,MES";

                    // Strategy settings
                    Calculate = Calculate.OnEachTick;
                    EntriesPerDirection = 100; // Allow multiple entries per direction for different instruments
                    EntryHandling = EntryHandling.AllEntries;
                    IsExitOnSessionCloseStrategy = false;
                    IsFillLimitOnTouch = false;
                    MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                    OrderFillResolution = OrderFillResolution.Standard;
                    Slippage = 0;
                    StartBehavior = StartBehavior.WaitUntilFlat;
                    TimeInForce = TimeInForce.Gtc;
                    TraceOrders = false;
                    RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                    StopTargetHandling = StopTargetHandling.PerEntryExecution;
                    BarsRequiredToTrade = 0;

                    LogMessage($"🔧 DiscordTradeCopier initializing (restart #{restartCount})", true);
                }
                else if (State == State.Configure)
                {
                    LogMessage($"⚙️ DiscordTradeCopier configuring", true);

                    // Add the primary instrument to the map
                    instrumentMap[Instrument.MasterInstrument.Name.ToUpper()] = 0;
                    LogMessage($"📈 Primary instrument: {Instrument.MasterInstrument.Name} at index 0", true);

                    // Add additional data series for other instruments
                    if (!string.IsNullOrEmpty(TradableInstruments))
                    {
                        string[] symbols = TradableInstruments.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        int addedCount = 0;
                        
                        foreach (string symbol in symbols)
                        {
                            string upperSymbol = symbol.Trim().ToUpper();
                            if (!instrumentMap.ContainsKey(upperSymbol) && upperSymbol != Instrument.MasterInstrument.Name.ToUpper())
                            {
                                try
                                {
                                    // Try to add data series with error handling
                                    AddDataSeries(upperSymbol, BarsPeriodType.Minute, 1);
                                    addedCount++;
                                    LogMessage($"📈 Adding data series for: {upperSymbol}", true);
                                }
                                catch (Exception ex)
                                {
                                    LogMessage($"⚠️ Failed to add data series for {upperSymbol}: {ex.Message}", true);
                                    // Continue with other symbols instead of failing completely
                                }
                            }
                            else if (upperSymbol == Instrument.MasterInstrument.Name.ToUpper())
                            {
                                LogMessage($"📈 {upperSymbol} is the primary instrument (already loaded)", true);
                            }
                        }
                        
                        LogMessage($"📊 Added {addedCount} additional data series", true);
                    }
                }
                else if (State == State.DataLoaded)
                {
                    try
                    {
                        LogMessage($"📊 DataLoaded phase started. BarsArray.Length: {BarsArray.Length}", true);
                        
                        // Clear and rebuild instrument map to avoid duplicates
                        instrumentMap.Clear();
                        
                        // Map all loaded instruments to their BarsArray index
                        for (int i = 0; i < BarsArray.Length; i++)
                        {
                            if (BarsArray[i] == null || BarsArray[i].Instrument == null)
                            {
                                LogMessage($"⚠️ Null instrument at index {i}", true);
                                continue;
                            }
                            
                            string symbolName = BarsArray[i].Instrument.MasterInstrument.Name.ToUpper();
                            instrumentMap[symbolName] = i; // Use assignment to avoid duplicates
                            LogMessage($"📈 Mapped instrument: {symbolName} to index {i}", true);
                        }
                        
                        LogMessage($"✅ DataLoaded completed. {instrumentMap.Count} instruments mapped.", true);
                        
                        // Validate we have at least one instrument
                        if (instrumentMap.Count == 0)
                        {
                            LogMessage($"❌ CRITICAL: No instruments mapped in DataLoaded!", true);
                            return;
                        }
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
                        LogMessage($"🚀 DiscordTradeCopier entering Active state on account: {Account.DisplayName}", true);
                        LogMessage($"📊 Available instruments: {instrumentMap.Count}", true);
                        
                        // Additional validation before starting listener
                        if (Account == null)
                        {
                            LogMessage($"❌ CRITICAL: Account is null!", true);
                            return;
                        }
                        
                        if (BarsArray == null || BarsArray.Length == 0)
                        {
                            LogMessage($"❌ CRITICAL: BarsArray is null or empty!", true);
                            return;
                        }
                        
                        // Validate that we have instruments mapped
                        if (instrumentMap.Count == 0)
                        {
                            LogMessage($"❌ CRITICAL: No instruments mapped! Check TradableInstruments parameter.", true);
                            return;
                        }
                        
                        // Check if account is connected
                        if (Account.Connection.Status != ConnectionStatus.Connected)
                        {
                            LogMessage($"⚠️ WARNING: Account connection status is {Account.Connection.Status}", true);
                            // Don't return here, as strategy might still work
                        }
                        
                        // Add a small delay before starting listener to ensure everything is ready
                        var startTimer = new System.Windows.Threading.DispatcherTimer();
                        startTimer.Interval = TimeSpan.FromSeconds(2);
                        startTimer.Tick += (s, e) => {
                            startTimer.Stop();
                            try
                            {
                                StartListener();
                                LogMessage($"✅ DiscordTradeCopier fully started and ready!", true);
                            }
                            catch (Exception startEx)
                            {
                                LogMessage($"❌ Error in delayed StartListener: {startEx.Message}", true);
                            }
                        };
                        startTimer.Start();
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"❌ CRITICAL ERROR in State.Active: {ex.Message}", true);
                        LogMessage($"❌ Active Stack trace: {ex.StackTrace}", true);
                        return; // Don't proceed
                    }
                }
                else if (State == State.Terminated)
                {
                    LogMessage($"🛑 DiscordTradeCopier terminating - cleaning up resources", true);
                    
                    try
                    {
                        StopListener();
                        
                        // Clear instrument map
                        instrumentMap.Clear();
                        
                        LogMessage($"✅ Strategy cleanup completed", true);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"❌ Error during termination cleanup: {ex.Message}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ FATAL ERROR in OnStateChange: {ex.Message}", true);
                Print($"❌ FATAL ERROR in OnStateChange: {ex.Message}");
            }
        }

        private void StartListener()
        {
            try
            {
                LogMessage($"🔧 StartListener called. Current listening status: {isListening}", true);
                
                // Stop any existing listener first
                if (tcpListener != null)
                {
                    LogMessage($"🔄 Stopping existing TCP listener", true);
                    tcpListener.Stop();
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
                tcpListener.Start();
                isListening = true;

                // Create and start the polling timer
                if (pollTimer != null)
                {
                    pollTimer.Stop();
                    pollTimer = null;
                }
                
                pollTimer = new System.Windows.Threading.DispatcherTimer();
                pollTimer.Interval = TimeSpan.FromMilliseconds(100);
                pollTimer.Tick += PollForCommands;
                pollTimer.Start();

                LogMessage($"✅ Discord Trade Copier READY! Listening on port {TcpPort}", true);
                LogMessage($"📈 Trading Instruments: {string.Join(", ", instrumentMap.Keys)}", true);
                LogMessage($"💬 Send commands like: BUY 1 NQ, SELL 2 ES, CLOSE MNQ, etc.", true);
                LogMessage($"🏦 Active Account: {Account.DisplayName}", true);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error starting TCP listener on port {TcpPort}: {ex.Message}", true);
                LogMessage($"❌ StartListener Stack trace: {ex.StackTrace}", true);
                
                // Check if port is in use
                if (ex.Message.Contains("address already in use") || ex.Message.Contains("Only one usage"))
                {
                    LogMessage($"⚠️ Port {TcpPort} is already in use. Try a different port or restart NinjaTrader.", true);
                }
                
                isListening = false;
                
                // Retry after 15 seconds with better error handling
                var retryTimer = new System.Windows.Threading.DispatcherTimer();
                retryTimer.Interval = TimeSpan.FromSeconds(15);
                retryTimer.Tick += (s, e) => {
                    retryTimer.Stop();
                    try
                    {
                        LogMessage($"🔄 Retrying TCP listener on port {TcpPort}...", true);
                        StartListener();
                    }
                    catch (Exception retryEx)
                    {
                        LogMessage($"❌ Retry failed: {retryEx.Message}", true);
                    }
                };
                retryTimer.Start();
            }
        }

        private void StopListener()
        {
            try
            {
                isListening = false;
                pollTimer?.Stop();
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

        private void PollForCommands(object sender, EventArgs e)
        {
            if (!isListening || tcpListener == null)
                return;

            try
            {
                if (tcpListener.Pending())
                {
                    LogMessage("📞 Incoming Discord command...");

                    using (TcpClient client = tcpListener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            LogMessage($"📨 Command: '{command}'");

                            ProcessDiscordCommand(command);

                            // Send response back to Discord bot
                            byte[] response = Encoding.UTF8.GetBytes("✅ Command received\n");
                            stream.Write(response, 0, response.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error receiving command: {ex.Message}");
            }
        }

        private void ProcessDiscordCommand(string command)
        {
            try
            {
                if (string.IsNullOrEmpty(command))
                {
                    LogMessage("❌ Empty command received");
                    return;
                }

                // Convert to uppercase and split by spaces, preserve original for parsing
                string upperCommand = command.ToUpper().Replace(",", "");
                string[] parts = upperCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                {
                    LogMessage("❌ Invalid command format");
                    return;
                }

                LogMessage($"🎯 Processing: {upperCommand} on account: {Account.DisplayName}");

                // Handle different command formats
                if (upperCommand.StartsWith("BUY") && upperCommand.Contains("STOP LIMIT @"))
                {
                    HandleBuyStopLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("SELL") && upperCommand.Contains("STOP LIMIT @"))
                {
                    HandleSellStopLimitCommand(parts);
                }
                else if (upperCommand.StartsWith("BUY"))
                {
                    HandleBuyMarketCommand(parts);
                }
                else if (upperCommand.StartsWith("SELL"))
                {
                    HandleSellMarketCommand(parts);
                }
                else if (upperCommand.StartsWith("CLOSE POSITION"))
                {
                    HandleClosePositionCommand(parts);
                }
                else if (upperCommand.StartsWith("CLOSE"))
                {
                    HandleCloseSymbolCommand(parts);
                }
                else if (upperCommand.StartsWith("MOVE SL TO"))
                {
                    HandleMoveSLCommand(parts);
                }
                else if (upperCommand.StartsWith("MOVE TP TO"))
                {
                    HandleMoveTPCommand(parts);
                }
                else
                {
                    // Legacy commands might not work as expected with multi-instrument.
                    // It's better to guide users to the new format.
                    LogMessage($"⚠️ Legacy command '{parts[0]}' used. Please use new format for multi-instrument trading.");
                    HandleLegacyCommand(parts);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error processing command: {ex.Message}");
            }
        }

        private int GetInstrumentIndex(string symbol)
        {
            string upperSymbol = symbol.ToUpper();
            if (instrumentMap.ContainsKey(upperSymbol))
            {
                return instrumentMap[upperSymbol];
            }

            LogMessage($"❌ Instrument '{symbol}' not found in tradable list. Add it to strategy parameters.", true);
            return -1; // Indicates instrument not found
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

        private void HandleBuyMarketCommand(string[] parts)
        {
            try
            {
                // Format: BUY # SYMBOL
                if (parts.Length >= 3 && int.TryParse(parts[1], out int quantity))
                {
                    string symbol = parts[2];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    LogMessage($"🟢 MARKET BUY: {quantity} {symbol}");
                    EnterLong(barsIndex, quantity, $"DiscordBuyMarket_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"❌ Invalid BUY format. Use: BUY # SYMBOL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in BUY command: {ex.Message}");
            }
        }

        private void HandleSellMarketCommand(string[] parts)
        {
            try
            {
                // Format: SELL # SYMBOL
                if (parts.Length >= 3 && int.TryParse(parts[1], out int quantity))
                {
                    string symbol = parts[2];
                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    LogMessage($"🔴 MARKET SELL: {quantity} {symbol}");
                    EnterShort(barsIndex, quantity, $"DiscordSellMarket_{symbol}_{DateTime.Now.Ticks}");
                }
                else
                {
                    LogMessage($"❌ Invalid SELL format. Use: SELL # SYMBOL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in SELL command: {ex.Message}");
            }
        }

        private void HandleCloseSymbolCommand(string[] parts)
        {
            try
            {
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
                            LogMessage($"❌ Invalid CLOSE format. Use: CLOSE # SYMBOL or CLOSE SYMBOL");
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

                    int barsIndex = GetInstrumentIndex(symbol);
                    if (barsIndex == -1) return;

                    if (quantity > 0)
                    {
                        LogMessage($"❌ CLOSING: {quantity} {symbol}");
                        ExitLong(barsIndex, quantity, $"Closing {quantity}", "");
                        ExitShort(barsIndex, quantity, $"Closing {quantity}", "");
                    }
                    else
                    {
                        LogMessage($"❌ CLOSING ALL for {symbol}");
                        ExitLong(barsIndex, "Closing All", "");
                        ExitShort(barsIndex, "Closing All", "");
                    }
                }
                else
                {
                    LogMessage($"❌ Invalid CLOSE format. Use: CLOSE # SYMBOL or CLOSE SYMBOL");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error in CLOSE command: {ex.Message}");
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
            if (EnableDebugLogging || forceLog)
            {
                Print($"{DateTime.Now:HH:mm:ss} - {message}");
            }
        }

        protected override void OnBarUpdate()
        {
            // Safety checks to prevent crashes
            if (State != State.Realtime && State != State.Historical) return;
            if (CurrentBars == null || CurrentBars.Length == 0) return;
            if (CurrentBars[0] < 1) return;
            if (BarsInProgress < 0 || BarsInProgress >= BarsArray.Length) return;

            try
            {
                // Heartbeat every 500 bars on the primary instrument ONLY
                if (BarsInProgress == 0 && CurrentBar % 500 == 0 && CurrentBar > 0)
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
                LogMessage($"📊 Position [{position.Instrument.MasterInstrument.Name}]: {marketPosition} {Math.Abs(quantity)} @ {averagePrice:F2}");
            }
        }
    }
}