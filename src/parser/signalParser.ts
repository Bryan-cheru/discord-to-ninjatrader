import { TradeSignal } from '../types';
import { logger } from '../utils/logger';

export class SignalParser {
  private readonly SIGNAL_PATTERNS = {
    // New format patterns
    // Pattern: BUY # MES STOP LIMIT @ ####, SELL # MES STOP LIMIT @ ####
    buyStopLimit: /^BUY\s+(\d+)\s+([A-Z]{2,4})\s+STOP\s+LIMIT\s+@\s+(\d+(?:\.\d+)?)$/i,
    sellStopLimit: /^SELL\s+(\d+)\s+([A-Z]{2,4})\s+STOP\s+LIMIT\s+@\s+(\d+(?:\.\d+)?)$/i,
    
    // Pattern: BUY # MES LIMIT @ ####, SELL # MES LIMIT @ #### (regular limit orders)
    buyLimit: /^BUY\s+(\d+)\s+([A-Z]{2,4})\s+LIMIT\s+@\s+(\d+(?:\.\d+)?)$/i,
    sellLimit: /^SELL\s+(\d+)\s+([A-Z]{2,4})\s+LIMIT\s+@\s+(\d+(?:\.\d+)?)$/i,
    
    // Pattern: BUY # MES, SELL # MES (market orders)
    buyMarket: /^BUY\s+(\d+)\s+([A-Z]{2,4})$/i,
    sellMarket: /^SELL\s+(\d+)\s+([A-Z]{2,4})$/i,
    
    // Pattern: CLOSE # MES
    closeSymbol: /^CLOSE\s+(\d+)\s+([A-Z]{2,4})$/i,
    
    // Pattern: CLOSE POSITION
    closePosition: /^CLOSE\s+POSITION$/i,
    
    // Pattern: CLOSE ALL (legacy)
    closeAll: /^CLOSE\s+ALL$/i,
    
    // Pattern: MOVE SL TO ####
    moveSL: /^MOVE\s+SL\s+TO\s+(\d+(?:\.\d+)?)$/i,
    
    // Legacy patterns for backward compatibility
    simpleMarket: /^(BUY|SELL|LONG|SHORT)\s+([A-Z0-9]{1,8})(?:\s+(\d+))?(?:\s+([\d.]+))?$/i,
    stopLoss: /^(?:SL|STOP|STOPLOSS)\s+([\d.]+)$/i,
    takeProfit: /^TP\s+(\d+(?:\.\d+)?)$/i,
    cancel: /^CANCEL$/i
  };

  public parseMessage(content: string, channelId: string): TradeSignal | null {
    const cleanContent = content.trim().toUpperCase();
    
    // Try each pattern
    for (const [patternName, pattern] of Object.entries(this.SIGNAL_PATTERNS)) {
      const match = cleanContent.match(pattern);
      if (match) {
        logger.info(`Signal matched pattern: ${patternName}`);
        return this.createTradeSignal(match, channelId, patternName as keyof typeof this.SIGNAL_PATTERNS);
      }
    }

    logger.debug(`No trade signal pattern matched for message: ${content}`);
    return null;
  }

  private createTradeSignal(match: RegExpMatchArray, channelId: string, patternType: keyof typeof this.SIGNAL_PATTERNS): TradeSignal {
    let action: 'BUY' | 'SELL' | 'CLOSE' | 'MOVE_SL' | 'MOVE_TP' | 'CLOSE_POSITION';
    let symbol: string = '';
    let price: number | undefined;
    let stopLoss: number | undefined;
    let takeProfit: number | undefined;
    let orderType: 'MARKET' | 'LIMIT' | 'STOP_LIMIT' | 'CLOSE' | 'MOVE_SL' | 'MOVE_TP' = 'MARKET';
    let quantity: number = 1;

    switch (patternType) {
      case 'simpleMarket':
        // Handle: BUY NQ, BUY NQ 2, BUY NQ 2 16800
        action = match[1].toUpperCase() === 'LONG' ? 'BUY' : 
                match[1].toUpperCase() === 'SHORT' ? 'SELL' : 
                match[1].toUpperCase() as 'BUY' | 'SELL';
        symbol = match[2];
        quantity = match[3] ? parseInt(match[3]) : 1;
        price = match[4] ? parseFloat(match[4]) : undefined;
        orderType = price ? 'LIMIT' : 'MARKET';
        break;

      case 'closePosition':
        // Handle: CLOSE, CLOSE ALL, CLOSE LONG, CLOSE SHORT
        action = 'CLOSE';
        const target = match[2];
        if (target === 'ALL' || !target) {
          action = 'CLOSE_POSITION';
          symbol = 'ALL';
        } else if (target === 'LONG' || target === 'SHORT') {
          symbol = target;
        }
        orderType = 'CLOSE';
        break;

      case 'stopLoss':
        // Handle: SL 4950
        action = 'MOVE_SL';
        stopLoss = parseFloat(match[1]);
        orderType = 'MOVE_SL';
        break;

      case 'takeProfit':
        // Handle: TP 5050
        action = 'MOVE_TP';
        takeProfit = parseFloat(match[1]);
        orderType = 'MOVE_TP';
        symbol = 'N/A';
        break;

      case 'buyStopLimit':
        // Handle: BUY 2 MES STOP LIMIT @ 4950
        action = 'BUY';
        quantity = parseInt(match[1]);
        symbol = match[2];
        price = parseFloat(match[3]);
        orderType = 'STOP_LIMIT';
        break;
        
      case 'sellStopLimit':
        // Handle: SELL 2 MES STOP LIMIT @ 4950
        action = 'SELL';
        quantity = parseInt(match[1]);
        symbol = match[2];
        price = parseFloat(match[3]);
        orderType = 'STOP_LIMIT';
        break;
        
      case 'buyLimit':
        // Handle: BUY 2 MES LIMIT @ 4950
        action = 'BUY';
        quantity = parseInt(match[1]);
        symbol = match[2];
        price = parseFloat(match[3]);
        orderType = 'LIMIT';
        break;
        
      case 'sellLimit':
        // Handle: SELL 2 MES LIMIT @ 4950
        action = 'SELL';
        quantity = parseInt(match[1]);
        symbol = match[2];
        price = parseFloat(match[3]);
        orderType = 'LIMIT';
        break;
        
      case 'buyMarket':
        // Handle: BUY 2 MES
        action = 'BUY';
        quantity = parseInt(match[1]);
        symbol = match[2];
        orderType = 'MARKET';
        break;
        
      case 'sellMarket':
        // Handle: SELL 2 MES
        action = 'SELL';
        quantity = parseInt(match[1]);
        symbol = match[2];
        orderType = 'MARKET';
        break;
        
      case 'closeSymbol':
        // Handle: CLOSE 2 MES
        action = 'CLOSE';
        quantity = parseInt(match[1]);
        symbol = match[2];
        orderType = 'CLOSE';
        break;
        
      case 'closeAll':
        // Handle: CLOSE ALL (legacy)
        action = 'CLOSE_POSITION';
        symbol = 'ALL';
        orderType = 'CLOSE';
        break;
        
      case 'moveSL':
        // Handle: MOVE SL TO 4950
        action = 'MOVE_SL';
        symbol = 'N/A';
        orderType = 'MOVE_SL';
        stopLoss = parseFloat(match[1]);
        break;

      case 'cancel':
        // Handle: CANCEL
        action = 'CLOSE_POSITION';
        symbol = 'CANCEL';
        orderType = 'CLOSE';
        break;

      default:
        throw new Error(`Unknown pattern type: ${patternType}`);
    }

    return {
      symbol,
      action,
      quantity,
      price,
      stopLoss,
      takeProfit,
      timestamp: new Date(),
      channelId,
      orderType
    };
  }

  public isValidSignal(signal: TradeSignal): boolean {
    // Special cases that don't need symbols
    if (signal.action === 'MOVE_SL' || signal.action === 'MOVE_TP' || signal.action === 'CLOSE_POSITION') {
      return true;
    }
    
    // Take profit signals (handled specially)
    if (signal.symbol === 'TP' && signal.takeProfit) {
      return true;
    }
    
    // Cancel signals  
    if (signal.symbol === 'CANCEL') {
      return true;
    }
    
    // Close commands with directional targets
    if (signal.action === 'CLOSE' && (signal.symbol === 'LONG' || signal.symbol === 'SHORT' || signal.symbol === 'ALL')) {
      return true;
    }

    // Basic validation for regular trades
    if (!signal.symbol || !signal.action) {
      return false;
    }

    // Validate symbol format (basic check) - allow single letters for futures
    if (!/^[A-Z0-9]{1,8}$/.test(signal.symbol)) {
      return false;
    }

    // Validate price values are positive
    if (signal.price && signal.price <= 0) {
      return false;
    }

    if (signal.stopLoss && signal.stopLoss <= 0) {
      return false;
    }

    if (signal.takeProfit && signal.takeProfit <= 0) {
      return false;
    }

    return true;
  }
}
