export interface TradeSignal {
  symbol: string;
  action: 'BUY' | 'SELL' | 'CLOSE' | 'MOVE_SL' | 'MOVE_TP' | 'CLOSE_POSITION';
  quantity: number;
  price?: number;
  stopLoss?: number;
  takeProfit?: number;
  timestamp: Date;
  channelId: string;
  orderType?: 'MARKET' | 'LIMIT' | 'STOP_LIMIT' | 'CLOSE' | 'MOVE_SL' | 'MOVE_TP';
}
