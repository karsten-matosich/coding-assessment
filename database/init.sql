CREATE TABLE IF NOT EXISTS accounts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  account_number TEXT NOT NULL UNIQUE,
  balance DECIMAL(10, 2) NOT NULL
);

INSERT OR IGNORE INTO accounts (name, account_number, balance)
VALUES 
  ('Account 1', '1', 1000.00),
  ('Account 2', '2', 2000.00),
  ('Account 3', '3', 3000.00);


CREATE TABLE IF NOT EXISTS transaction_uploads (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  upload_date DATETIME NOT NULL,
  file_name TEXT NOT NULL,
  file_size INTEGER NOT NULL,
  incoming_transaction_count INTEGER NOT NULL,
  outgoing_transaction_count INTEGER NOT NULL,
  status TEXT NOT NULL,
  error_message TEXT
);


CREATE TABLE IF NOT EXISTS transactions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  account_id INTEGER NOT NULL,
  transaction_upload_id INTEGER,
  amount DECIMAL(10, 2) NOT NULL,
  transaction_date DATETIME NOT NULL,
  direction TEXT NOT NULL,
  external_transaction_id TEXT,
  FOREIGN KEY (account_id) REFERENCES accounts (id),
  FOREIGN KEY (transaction_upload_id) REFERENCES transaction_uploads (id)
);

CREATE TABLE IF NOT EXISTS failed_transaction_imports (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  transaction_upload_id INTEGER NOT NULL,
  external_transaction_id TEXT NOT NULL,
  error_message TEXT NOT NULL,
  csv_row_value TEXT NOT NULL,
  FOREIGN KEY (transaction_upload_id) REFERENCES transaction_uploads (id)
)