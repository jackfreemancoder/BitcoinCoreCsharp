
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
### Parse a WIF and Get the Corresponding Address
```csharp
string wif = "yourWIFstringHere";
var network = Network.Main;
Key key = Key.Parse(wif, network);
var address = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, network);
Console.WriteLine($"Address: {address}");

```
### Generate a BIP39 Mnemonic and Derive Addresses
```csharp
var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
Console.WriteLine($"Mnemonic: {mnemonic}");

var seed = mnemonic.DeriveSeed();
var masterKey = new ExtKey(seed);
var key = masterKey.Derive(new KeyPath("44'/0'/0'/0/0"));

Console.WriteLine($"Address: {key.PrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main)}");

```
