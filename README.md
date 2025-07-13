# Bitcoin Core (.NET)

**Bitcoin Core** is a comprehensive and feature-rich Bitcoin library for C# and .NET developers. It enables seamless integration with the Bitcoin protocol, offering full support for transaction creation, wallet operations, script handling, and much more.

## Features

- ✅ Full Bitcoin protocol support (P2P, network messages, blocks, transactions)
- 🔐 Transaction creation, signing, verification, and broadcasting
- 🗝️ Address and key management (HD wallets, private/public keys, address formats)
- 📜 Bitcoin Script support (P2PKH, P2SH, Multisig, custom scripts)
- ⚡ SegWit (P2WPKH, P2WSH) and Taproot (P2TR) ready
- 📝 PSBT (Partially Signed Bitcoin Transactions) support
- 💸 Fee estimation and coin selection tools
- 🌐 Custom network support (mainnet, testnet, regtest, custom chains)
- 🔍 Blockchain parsing and analysis utilities
- 🧠 Smart contract and scripting capabilities
- 📦 BIP standards support (BIP32, BIP39, BIP44)
- 🔧 Utilities for Bitcoin data formats (Base58, Bech32, etc.)
- 
## Getting Started

Install via NuGet:

```
Install-Package BitcoinCore
```

Or with .NET CLI:

```
dotnet add package BitcoinCore
```
## Examples

### Derive Bitcoin address from bip 39 mnemonic phrase
```csharp

        // Your BIP39 mnemonic phrase (12 or 24 words)
        string mnemonicPhrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        // Create a Mnemonic object (using English wordlist by default)
        var mnemonic = new Mnemonic(mnemonicPhrase);

        // Optionally, provide a passphrase (empty string if none)
        string passphrase = "";

        // Generate seed from mnemonic + passphrase
        var seed = mnemonic.DeriveSeed(passphrase);

        // Create ExtKey from seed (BIP32 root key)
        ExtKey masterKey = new ExtKey(seed);

        // Choose network (MainNet or TestNet)
        Network network = Network.Main;

        // Derive the first account external chain key using BIP44 path: m/44'/0'/0'/0/0
        // m / purpose' / coin_type' / account' / change / address_index
        var keyPath = new KeyPath("44'/0'/0'/0/0");

        // Derive the key at that path
        ExtKey key = masterKey.Derive(keyPath);

        // Get the private key
        Key privateKey = key.PrivateKey;

        // Get the corresponding Bitcoin address (Legacy P2PKH)
        BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

        Console.WriteLine($"Address: {address}");
        Console.WriteLine($"Private Key (WIF): {privateKey.GetWif(network)}");
```
### Transaction Builder Example
```csharp
using BitcoinCore;
using System;
using System.Net;

class Program
{
    static void Main()
    {
        // Network selection
        var network = Network.Main;

        //Your WIF
        string wif = "KzgNNr37PgMSWv561THYBiA9AYe2BR46c5zq8CBDc122BQyTKeA3";

        Key senderPrivateKey = Key.Parse(wif, Network.Main);
        var senderAddress = senderPrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

        // Define the recipient's address
        var recipientAddress = BitcoinAddress.Create("1BgzswdwEQ45SFBTVqp5ivBnJ4EqEsHaHn", network); // Replace with actual recipient address

        // Fetch UTXO (Unspent Transaction Output) for the sender
        var utxoTxId = new uint256("your-transaction-id"); // Replace with actual transaction ID
        var utxoIndex = 0; // Replace with actual output index
        var utxoAmount = Money.Coins(0.001m); // Replace with actual amount in BTC

        // Create the transaction builder
        var txBuilder = network.CreateTransactionBuilder();

        // Add input (spending the UTXO)
        txBuilder.AddKeys(senderPrivateKey);
        txBuilder.AddCoins(new Coin(utxoTxId, (uint)utxoIndex, utxoAmount, senderAddress.ScriptPubKey));


        // Add output (sending to recipient)
        txBuilder.Send(recipientAddress, utxoAmount - Money.Satoshis(1000)); // Subtracting a small fee

        // Set the transaction fee
        txBuilder.SendFees(Money.Satoshis(1000)); // Adjust fee as necessary

        // Set the change address
        txBuilder.SetChange(senderAddress);

        // Build and sign the transaction
        var transaction = txBuilder.BuildTransaction(true);

        // Output the raw transaction hex
        Console.WriteLine(transaction.ToHex());
        Console.ReadLine();
    }
}

```

## Target Frameworks

- .NET Standard 2.0+
- .NET Core / .NET 5+

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

Contributions, issues, and feature requests are welcome!

## Disclaimer

This library is provided as-is with no warranties. Always test thoroughly before deploying in production environments.
