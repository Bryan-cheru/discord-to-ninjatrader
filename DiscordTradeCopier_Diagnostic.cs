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
    public class DiscordTradeCopier_Diagnostic : Strategy
    {
        #region Properties
        [NinjaScriptProperty]
        [Range(1024, 65535)]
        [Display(Name = "TCP Port", Order = 1, GroupName = "Connection")]
        public int TcpPort { get; set; } = 36973;
        #endregion

        protected override void OnStateChange()
        {
            try
            {
                Print($"🔍 DIAGNOSTIC: OnStateChange called with State: {State}");
                
                if (State == State.SetDefaults)
                {
                    Print($"🔍 DIAGNOSTIC: Entering SetDefaults");
                    
                    Description = "Discord Trade Copier - Diagnostic Version";
                    Name = "DiscordTradeCopier_Diagnostic";

                    // Minimal strategy settings
                    Calculate = Calculate.OnEachTick;
                    EntriesPerDirection = 1;
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

                    Print($"✅ DIAGNOSTIC: SetDefaults completed successfully");
                }
                else if (State == State.Configure)
                {
                    Print($"🔍 DIAGNOSTIC: Entering Configure");
                    Print($"🔍 DIAGNOSTIC: Primary Instrument: {Instrument?.MasterInstrument?.Name ?? "NULL"}");
                    Print($"✅ DIAGNOSTIC: Configure completed successfully");
                }
                else if (State == State.DataLoaded)
                {
                    Print($"🔍 DIAGNOSTIC: Entering DataLoaded");
                    Print($"🔍 DIAGNOSTIC: BarsArray.Length: {BarsArray?.Length ?? 0}");
                    
                    if (BarsArray != null && BarsArray.Length > 0)
                    {
                        for (int i = 0; i < BarsArray.Length; i++)
                        {
                            Print($"🔍 DIAGNOSTIC: BarsArray[{i}]: {BarsArray[i]?.Instrument?.MasterInstrument?.Name ?? "NULL"}");
                        }
                    }
                    
                    Print($"✅ DIAGNOSTIC: DataLoaded completed successfully");
                }
                else if (State == State.Active)
                {
                    Print($"🔍 DIAGNOSTIC: Entering Active");
                    Print($"🔍 DIAGNOSTIC: Account: {Account?.DisplayName ?? "NULL"}");
                    Print($"🔍 DIAGNOSTIC: Account Connection Status: {Account?.Connection?.Status ?? ConnectionStatus.Disconnected}");
                    Print($"🔍 DIAGNOSTIC: BarsArray Length: {BarsArray?.Length ?? 0}");
                    
                    // Try to start a simple TCP listener
                    try
                    {
                        Print($"🔍 DIAGNOSTIC: Attempting to create TCP listener on port {TcpPort}");
                        var testListener = new TcpListener(IPAddress.Any, TcpPort);
                        testListener.Start();
                        Print($"✅ DIAGNOSTIC: TCP listener created successfully on port {TcpPort}");
                        testListener.Stop();
                        Print($"✅ DIAGNOSTIC: TCP listener stopped successfully");
                    }
                    catch (Exception tcpEx)
                    {
                        Print($"❌ DIAGNOSTIC: TCP listener failed: {tcpEx.Message}");
                        Print($"❌ DIAGNOSTIC: TCP Error Details: {tcpEx.StackTrace}");
                    }
                    
                    Print($"✅ DIAGNOSTIC: Active state completed successfully");
                }
                else if (State == State.Terminated)
                {
                    Print($"🔍 DIAGNOSTIC: Entering Terminated");
                    Print($"✅ DIAGNOSTIC: Terminated completed successfully");
                }
            }
            catch (Exception ex)
            {
                Print($"❌ DIAGNOSTIC: CRITICAL ERROR in OnStateChange: {ex.Message}");
                Print($"❌ DIAGNOSTIC: Error Details: {ex.StackTrace}");
                Print($"❌ DIAGNOSTIC: State when error occurred: {State}");
                throw; // Re-throw to see the actual error in NT8
            }
        }

        protected override void OnBarUpdate()
        {
            // Minimal bar update - just log every 1000 bars
            if (BarsInProgress == 0 && CurrentBar % 1000 == 0 && CurrentBar > 0)
            {
                Print($"💓 DIAGNOSTIC: Heartbeat - Bar {CurrentBar}");
            }
        }
    }
}
