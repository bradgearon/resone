CREATE TABLE licenses (
 id TEXT PRIMARY KEY,
 provider TEXT NOT NULL,
 purchase_id TEXT NOT NULL,
 key_hash TEXT NOT NULL UNIQUE,
 status TEXT NOT NULL DEFAULT 'active' CHECK(status IN ('active','revoked')),
 device TEXT,
 released INTEGER NOT NULL DEFAULT 0 CHECK(released IN (0,1)),
 lease_until INTEGER NOT NULL DEFAULT 0,
 created_at INTEGER NOT NULL,
 UNIQUE(provider, purchase_id)
);
CREATE TABLE used_nonces (nonce TEXT PRIMARY KEY, expires_at INTEGER NOT NULL);
CREATE INDEX nonce_expiry ON used_nonces(expires_at);
