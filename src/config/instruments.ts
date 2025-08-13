export interface InstrumentConfig {
  symbol: string;
  nt8Name: string;
  tickSize: number;
  contractSize: number;
}

export const INSTRUMENT_MAPPING: Record<string, InstrumentConfig> = {
  // Futures - Update contract months as needed
  'NQ': {
    symbol: 'NQ',
    nt8Name: 'NQ 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 0.25,
    contractSize: 20
  },
  'ES': {
    symbol: 'ES',
    nt8Name: 'ES 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 0.25,
    contractSize: 50
  },
  'MES': {
    symbol: 'MES',
    nt8Name: 'MES 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 0.25,
    contractSize: 5
  },
  'MNQ': {
    symbol: 'MNQ',
    nt8Name: 'MNQ 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 0.25,
    contractSize: 2
  },
  'YM': {
    symbol: 'YM',
    nt8Name: 'YM 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 1.0,
    contractSize: 5
  },
  'RTY': {
    symbol: 'RTY',
    nt8Name: 'RTY 12-25', // December 2025 - UPDATE THIS QUARTERLY
    tickSize: 0.1,
    contractSize: 50
  },
  // Forex - these don't expire
  'EURUSD': {
    symbol: 'EURUSD',
    nt8Name: 'EUR/USD',
    tickSize: 0.00001,
    contractSize: 100000
  },
  'GBPUSD': {
    symbol: 'GBPUSD',
    nt8Name: 'GBP/USD',
    tickSize: 0.00001,
    contractSize: 100000
  },
  'USDJPY': {
    symbol: 'USDJPY',
    nt8Name: 'USD/JPY',
    tickSize: 0.001,
    contractSize: 100000
  }
};

export function getInstrumentConfig(symbol: string): InstrumentConfig | null {
  return INSTRUMENT_MAPPING[symbol.toUpperCase()] || null;
}

export function getNT8InstrumentName(symbol: string): string {
  const config = getInstrumentConfig(symbol);
  return config ? config.nt8Name : symbol;
}
