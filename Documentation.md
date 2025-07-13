
# BitcoinCore Extension Documentation

## Overview
BitcoinCore is a comprehensive .NET library for handling Bitcoin operations. This extension documentation covers all primary functionalities, types, and usages to help developers integrate BitcoinCore into their projects effectively.

---

## Installation
To install BitcoinCore, use NuGet Package Manager:

```bash
Install-Package BitcoinCore
```

Or via .NET CLI:

```bash
dotnet add package BitcoinCore
```

---

## Namespaces

to:

```csharp
using BitcoinCore;
```

---

## Core Classes

### BitcoinAddress
Represents a Bitcoin address.

```csharp
BitcoinAddress address = BitcoinAddress.Create("1BitcoinAddress...");
```

### Key
Represents a private key.

```csharp
Key key = new Key(); // generates a new private key
PubKey pubKey = key.PubKey;
BitcoinSecret secret = key.GetBitcoinSecret(Network.Main);
```

### Script
Handles Bitcoin Script operations.

```csharp
Script scriptPubKey = address.ScriptPubKey;
```

### Transaction
Represents a Bitcoin transaction.

```csharp
Transaction tx = new Transaction();
tx.Version = 1;
tx.Inputs.Add(new TxIn(...));
tx.Outputs.Add(new TxOut(...));
```

### Network
Specifies the Bitcoin network (Mainnet, Testnet, RegTest).

```csharp
Network network = Network.Main;
```

---

## Key Features

### Creating Addresses

```csharp
Key key = new Key();
BitcoinAddress address = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);
```

### Parsing Addresses

```csharp
BitcoinAddress address = BitcoinAddress.Create("1BitcoinAddress...", Network.Main);
```

### Generating Transactions

```csharp
Transaction tx = new Transaction();
tx.Inputs.Add(new TxIn(prevOutPoint));
tx.Outputs.Add(new TxOut(Money.Coins(0.1m), recipientAddress.ScriptPubKey));
```

### Signing Transactions

```csharp
TransactionBuilder builder = Network.Main.CreateTransactionBuilder();
builder.AddCoins(coins);
builder.AddKeys(key);
builder.SignTransactionInPlace(tx);
```

### Broadcasting Transactions
Use your preferred Bitcoin node or API to broadcast the serialized transaction.

```csharp
var rawTx = tx.ToHex();
// Broadcast rawTx to the Bitcoin network using your preferred method
```

---

## Additional Types and Utilities

### Money
Represents Bitcoin amount.

```csharp
Money amount = Money.Coins(0.01m);
long satoshis = amount.Satoshi;
```

### TxIn and TxOut
Inputs and outputs for transactions.

```csharp
TxIn input = new TxIn(outpoint);
TxOut output = new TxOut(Money.Coins(0.01m), address.ScriptPubKey);
```

### OutPoint
Reference to a previous transaction output.

```csharp
OutPoint outPoint = new OutPoint(uint256.Parse("txid"), 0);
```

### PubKey
Represents a public key.

```csharp
PubKey pubKey = key.PubKey;
```

### TransactionBuilder
Helps build and sign transactions.

```csharp
TransactionBuilder builder = Network.Main.CreateTransactionBuilder();
builder.AddCoins(coins);
builder.AddKeys(key);
builder.Send(address, Money.Coins(0.1m));
builder.SetChange(changeAddress);
builder.BuildTransaction(sign: true);
```

---

## Network Parameters

- `Network.Main` — Bitcoin mainnet
- `Network.TestNet` — Bitcoin testnet
- `Network.RegTest` — Local regression testing network

---

## Common Patterns

### Generate a new private key and address

```csharp
Key key = new Key();
BitcoinSecret secret = key.GetBitcoinSecret(Network.Main);
BitcoinAddress address = secret.GetAddress(ScriptPubKeyType.Legacy);
Console.WriteLine($"Address: {address}");
Console.WriteLine($"Private Key: {secret}");
```

### Create and sign a transaction

```csharp
TransactionBuilder builder = Network.Main.CreateTransactionBuilder();
builder.AddCoins(coins);
builder.AddKeys(key);
builder.Send(recipientAddress, Money.Coins(0.1m));
builder.SetChange(changeAddress);
Transaction tx = builder.BuildTransaction(sign: true);
bool verified = builder.Verify(tx);
Console.WriteLine($"Transaction verified: {verified}");
```

### Parse and verify a transaction from hex

```csharp
Transaction tx = Transaction.Parse(txHex, Network.Main);
bool isVerified = tx.CheckTransaction();
```

---

## Helper Extensions

- `GetAddress()` extension on `PubKey` to get Bitcoin address.
- `GetBitcoinSecret()` extension on `Key` to get private key in WIF format.
- `CreateTransactionBuilder()` on `Network` to instantiate a transaction builder.

---

## Frequently Asked Questions

**Q:** How to switch networks?  
**A:** Use the `Network` property throughout your operations, e.g., `Network.TestNet` for testnet.

**Q:** How do I convert between satoshis and BTC?  
**A:** Use `Money.Coins(decimal)` for BTC to satoshi and `Money.Satoshi` to get satoshis.

**Q:** Can I create SegWit addresses?  
**A:** Yes, use `ScriptPubKeyType.Segwit` or `ScriptPubKeyType.SegwitP2SH` in `GetAddress()`.

---

---

## License

This project is licensed under the MIT License.
