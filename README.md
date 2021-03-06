# THE PLUGIN IS IN ALPHA STATE, EXPECT HEAVY CHANGES

# About

This NeosVR plugin is used to provide remote Logix programming
support, through external scripts sent through Websocket.

# Compilation and installation

You'll most likely need to change the DLL Dependencies of the project, and
set them to appropriate paths on your system.   
I'll try to put in place a way to automate this process (CMake maybe ?).  
Meanwhile, be sure to reference the following DLL, from `Neos_Data\Managed\`
inside NeosVR installation directory :

* BaseX.dll
* CloudX.Shared.dll
* CodeX.dll
* FrooxEngine.dll

Then **Generate the solution** and copy the generated DLL files, located
in the source folder `bin/Debug/netstandard2.0` or `bin/Release/netstandard2.0`
to NeosVR `Libraries` directory located in the installation folder.

So, if NeosVR is installed in :
`C:\Program Files (x86)\Steam\steamapps\common\NeosVR`

And the source folder is located in `%HOMEPATH%\sources\voyage-neosvr-plugin`

You'll have to be sure that the following dependencies are referenced :
* `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Neos_Data\Managed\BaseX.dll`
* `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Neos_Data\Managed\CloudX.Shared.dll`
* `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Neos_Data\Managed\CodeX.dll`
* `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Neos_Data\Managed\ FrooxEngine.dll`

The generate the solution (we'll use the Debug configuration in this example) and then :

```powershell
copy %HOMEPATH%\sources\voyage-neosvr-plugin\bin\Debug\netstandard2.0\VoyageNeosVRPlugin.* C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Libraries
```

# Usage

## Start NeosVR with the plugin

Once the plugin copied in the `Libraries` directory inside NeosVR installation
folder, launch the game using the **Neos Launcher**, then be sure to check
`NeosSampleLibrary.dll` in the list before clicking `Launch`.

## Add the component

Inside the game, open the inspector and `Add a component`, then select :
**Voyage** > **RemoteLogix**

Once the component added, setup the websocket URI to which the
component should receive scripts from, then click on `Connect`.

> The default URI targets the Websocket server started by the desktop
> version of the [RemoteLogix editor](https://github.com/vr-voyage/remote-logix)

Once the component, you can start streaming LogiX script to NeosVR.  
Each program will be added as a child of the slot which host the component.

> If you send the same program twice, it will create two children with the
> same name.
> There is no updating or overwriting mechanism at the moment.

Once the program uploaded, you can remove the component and restart
the game, to share the generated programs with your friends.

# Limitations

## Multiplayer will most likely be broken

On NeosVR, when you use a custom plugin, you can only join people using
the same custom plugin. So you're most likely going to be alone when
using this plugin, at the moment.

## No overwriting or updating mechanism

Uploading the same program multiple times just generate multiple copies
of the program.

## Remaining Websocket clients are useless

When you click on 'Connect', a WebsocketClient component is added, with
specific handlers setup internally to receive the data and parse it through the
plugin.  
If you disconnect and the reconnect to the game, the added WebsocketClient
component will still be there, but the custom handlers won't be restored,
making the client useless.  
I'll see how to fix that in the future. Meanwhile, remove remaining websocket
generated by the plugin when reconnecting.

> During a  continuoussession, unchecking 'Connect' after checking it
> will remove the WebsocketClient without issues.

# Script syntax

## Basics

* Each line begins with an instruction mnemonic.
* Instruction arguments are separated by spaces.
* Single quoted arguments contain C# class names and input/output slots names.  
  They're expected to not contain spaces.  
  If any single-quote character present in these identifiers are doubled.
* Double quoted arguments contain user-input encoded in Base64.
* Numeric arguments are printed as-is.


## Documentation syntax

In this documentation :

 * `"NAME"` means a double quoted Base64 encoded user input, referenced
 as NAME in the docs.  
   To decode it, you're expected to remove the double quotes and decode
  the content, using standard Base64 decoding algorithm.

 * `'FullClassName'`, `'Input'`, `'Output'` mean single-quoted C# full class names 
 and nodes input/output names as printed inside NeosVR.  
 To parse these strings, remove the surrounding single quotes and every two
 occurences of single-quote characters by one single-quote character within
 the remaining string.  
 For the C# class names, you'll need to check the **FrooxEngine.dll** assembly.  
 Visual Studio can do that, by default.  
 For example, the full class name of `StringInput` is 
 `FrooxEngine.LogiX.Input.StringInput`.
 
 > This single quote handling might be removed in the future, since no identifier
 > use single quotes.

 * Non quoted strings are either mnemonics or numeric arguments names.  
 These names should be replaced by actual numbers in the script.


## Mnemonics

### PROGRAM "[BASE64_INPUT]" VERSION

Define the program name.

Arguments :
* The program name encoded in base64.
* The script version used. Should always be 1 at the moment.

> The script version is used by the interpreter to understand how
> to interpret the following script.

Example :

`PROGRAM "RXhhbXBsZQ==" 1`

### NODE ID 'NodeFullClassName'  "NODE_NAME"

Define a new node.

Arguments :
* The node ID. Used as reference in the following instructions. MUST be unique.
* The node full C# class name. Example : 'FrooxEngine.LogiX.Input.StringInput' for **StringInput**.
* The node name, encoded in Base64.

> The best way to get class names is to add `FrooxEngine.dll` in a DLL project,
> through Visual Studio, and explore the `FrooxEngine.LogiX` assemblies.  
> To do that, on the right-pane of Visual Studio, right-click `FrooxEngine` in
> **Dependencies** > **Assemblys**.

Example :

`NODE 17 'FrooxEngine.LogiX.Input.StringInput' "U3RyaW5nSW5wdXQ="`

### SETCONST NODE_ID "CONTENT"

Defines the content of a constant node.

Arguments :
* The node ID representing a constant value node. The node ID MUST be defined through a `NODE` instruction before.
* The base64 encoded content.

Example :

`SETCONST 17 "QmxlbmRzaGFwZXM="`

### POS NODE_ID X Y

Defines the position of a node.

> Note that RemoteLogix encode Y positions with Y pointing downward.
> This is why the plugin actually negate the value.

Arguments :
* The node ID. The node ID MUST be defined through a `NODE` instruction before.
* The X position of the node.
* The Y position of the node.

Example :

`POS 17 1280 280`

### INPUT TO_NODE_ID 'InputName' FROM_NODE_ID 'OutputName'

Defines a connection between two nodes.

Arguments :
* The node ID receiving the connection. The node ID MUST be defined through a `NODE` instruction before.
* The name of the input slot receiving the connection.
* The node ID from where the connection starts. The node ID MUST be defined through a `NODE` instruction before.
* The name of the output slot from where the connection starts.

> `'*'` output name is used for default outputs on various nodes with only
> one output slot.

Example :

`INPUT 15 'Name' 17 '*'`

Connects node 17 "*" OUTPUT to INPUT "Name" on node 15 .

### IMPULSE TO_NODE_ID 'ImpulseTarget' FROM_NODE_ID 'Impulse'

Connects Impulse slots between two nodes.

* The node ID receiving the connection. The node ID MUST be defined through a `NODE` instruction before.
* The name of the Impulse input receving the connection.
* The node ID from where the connection starts. The node ID MUST be defined through a `NODE` instruction before.
* The name of the Impulse output from where the connection start.

`IMPULSE 46 'Run' 10 'True'`

Connects node 10 "True" Impulse OUTPUT to the "True" Impulse INPUT on node 46 .
