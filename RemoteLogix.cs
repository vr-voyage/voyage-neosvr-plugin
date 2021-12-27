using System;
using BaseX;
using FrooxEngine;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.Input;
using FrooxEngine.LogiX;
using System.Reflection;

namespace VoyageNeosVRPlugin
{
    [Category("Voyage")]
    public class RemoteLogix : Component
    {

        /*public readonly SyncRef<SkinnedMeshRenderer> skinRef;
        public readonly SyncRef<Slot> otherSlot;
        public readonly SyncRef<Slot> testEmotionSlot;*/
        public readonly Sync<string> websocketServerAddress;
        public readonly Sync<bool> connect;

        private readonly string systemAssemblyName = typeof(string).Assembly.FullName;
        private readonly string frooxAssemblyName  = typeof(FrooxEngine.LogiX.LogixNode).Assembly.FullName;
        private readonly string basexAssemblyName  = typeof(BaseX.AnimX).Assembly.FullName;
        private readonly string codexAssemblyName  = typeof(CodeX.AudioX).Assembly.FullName;
        private readonly string cloudxAssemblyName = typeof(CloudX.Shared.AssetInfo).Assembly.FullName;

        private Slot baseSlot;
        private Slot programSlot;
        private Slot logixSlot;
        private System.Collections.Generic.Dictionary<string, LogixNode> programNodes;
        private System.Collections.Generic.Dictionary<string, Slot> programSlots;
        private Slot currentSlot;

        private WebsocketClient addedClient;

        const char scriptFieldSeparator = ' ';

        private string[] websocketTextData = new string[32];
        private int lastReadTextIdx = 0;
        /* Rename this... this isn't the current idx, this
         * is the limit
         */
        private int currentTextIdx = 0;

        private bool WebsocketHasNewData()
        {
            return lastReadTextIdx != currentTextIdx;
        }

        private string WebsocketNewData()
        {
            string toRead = "";
            int i = lastReadTextIdx;
            while (i != currentTextIdx)
            {
                toRead += websocketTextData[i];
                i += 1;
                i &= 31;
            }
            lastReadTextIdx = i;
            return toRead;
        }

        private void WebsocketSaveMessage(string message)
        {
            int i = currentTextIdx;
            websocketTextData[i] = message;
            i += 1;
            i &= 31;
            currentTextIdx = i;
        }

        /* Remaining characters captured from the websocket, that
         * still doesn't form an entire line that could be parsed.
         */
        private string remainsFromWS = "";
        protected override void OnAttach()
        {
            base.OnAttach();
            UniLog.Log("Help I'm attached ! Send someone !");
            UniLog.Log("My Slot Name is : " + Slot.Name);

            if (websocketServerAddress.Value == null || websocketServerAddress.Value == "")
            {
                websocketServerAddress.Value = "ws://localhost:9080";
            }

            baseSlot = Slot;

            /*var register = Slot.AttachComponent<FrooxEngine.LogiX.Data.ValueRegister<bool>>();
            var writer   = Slot.AttachComponent<FrooxEngine.LogiX.Actions.WriteValueNode<bool>>();
            var refnode = Slot.AttachComponent<FrooxEngine.LogiX.ReferenceNode<IValue<bool>>>();
            refnode.RefTarget.TrySet(register);
            writer.Target.TrySet(refnode);*/

        }

        protected override void OnDestroy()
        {
            WebsocketSlotRemove();
        }

        private void WebsocketSlotAdd(Slot slot, string address)
        {
            var ws = slot.AttachComponent<WebsocketClient>();
            ws.BinaryMessageReceived += MyyWebsocketMessageBinary;
            ws.TextMessageReceived += MyyWebsocketMessageText;
            ws.Error += MyyWebsocketError;
            ws.Closed += MyyWebsocketClosed;
            ws.Connected += MyyWebsocketConnected;
            ws.URL.Value = new Uri(address);
            ws.HandlingUser.Target = ws.LocalUser;
            addedClient = ws;
        }

        private void WebsocketSlotRemove()
        {
            if (addedClient != null)
            {
                addedClient.Destroy();
            }
        }

        

        private DynamicValueVariableDriver<T> AddDynVarDriverFor<T>(
            Slot s,
            string name,
            IField<T> field,
            bool forceLink = false)
        {
            
            DynamicValueVariableDriver<T> dynVariable =
                s.AttachComponent<DynamicValueVariableDriver<T>>();
            dynVariable.VariableName.Value = name;
            dynVariable.Target.Target = field;
            /* If a field is already driven, you can only use ForceLink
             * to replace the current driver.
             * This is rather dangerous, as that breaks previous links
             * and I'm not sure 'Undo' will be able to recover from
             * this.
             */
            if (dynVariable.Target.Target == null)
            {
                if (!forceLink)
                {
                    s.RemoveComponent(dynVariable);
                    dynVariable = null;
                }
                else
                {
                    /* FIXME ! Untested ! */
                    dynVariable.Target.ForceLink(field);
                }
            }
            return dynVariable;
        }

        private DynamicValueVariable<T> AddDynVar<T>(Slot slot, string name)
        {
            var dynVarSpace = slot.AttachComponent<DynamicVariableSpace>();
            dynVarSpace.SpaceName.Value = name;

            DynamicValueVariable<T> value = slot.AttachComponent<DynamicValueVariable<T>>();
            value.VariableName.Value = name;

            return value;
        }

        Type LogixGetType(string typeFullName)
        {
            string searchedName = typeFullName + ", " + frooxAssemblyName;
            Type ret = Type.GetType(searchedName);
            UniLog.Log("Searched for : " + searchedName + "\nGot :\n");
            UniLog.Log(ret);
            return ret;
        }

        LogixNode LogixFieldFullName(Slot slot, string nodeClassFullName)
        {
            /*string searchName =
                nodeClassFullName + ", " + logixAssemblyName;
            UniLog.Log("LogixField GetType " + searchName);*/
            Type logixNodeType = ScriptGetActualNodeType(nodeClassFullName); //Type.GetType(searchName);
            LogixNode ret = null;
            if (logixNodeType != null)
            {
                /* FIXME Check if the type is compatible before casting... */
                ret = (LogixNode)slot.AttachComponent(logixNodeType);
            }
            else
            {
                UniLog.Log("Could not get the type of the node : " + nodeClassFullName);
            }
            return ret;
        }

        private void PrintIfNull(object o, string identifier)
        {
            if (o == null)
            {
                UniLog.Log($"{identifier} is NULL");
            }
        }

        void LogixConnectInputTo(
            LogixNode toNode,
            string inputName,
            LogixNode fromNode,
            string outputName = "*")
        {

            /* FIXME Check if the type is compatible before casting... */
            var input = toNode.TryGetField(inputName);

            if (input == null)
            {
                UniLog.Log($"Input {inputName} not found on {toNode.GetType()}");
                return;
            }
            
            if (outputName == "*")
            {
                ((ISyncRef)input).TrySet(fromNode);
            }
            else
            {
                /* Handle getting output from fromNode */
                ((ISyncRef)input).TrySet(fromNode.GetSyncMember(outputName));
            }
        }

        void LogixConnectImpulse(
            LogixNode fromNode,
            string impulseOutputName,
            LogixNode toNode,
            string toActionName)
        {

            UniLog.Log("Connecting IMPULSE !");
            UniLog.Log($"Getting Delegate {toActionName} from {toNode.Name}");
            Action toAction = (Action)Delegate.CreateDelegate(
                typeof(Action),
                toNode,
                toActionName);
            /* FIXME Check the type before casting... */
            var fromImpulse = fromNode.TryGetField(impulseOutputName);
            if (fromImpulse != null)
            {
                ((Impulse)fromImpulse).Target = toAction;
            }
        }

        Slot LogixProgramCreate(Slot mainSlot, string programName)
        {
            UniLog.Log("LogixProgramCreate");
            Slot programSlot = mainSlot.AddSlot(programName);
            logixSlot = programSlot.AddSlot("LogiX");
            UniLog.Log("End of LogixProgramCreate");
            return programSlot;
        }

        LogixNode LogixProgramAddNode(Slot program, string nodeName, string nodeType)
        {
            UniLog.Log("LogixProgramAddNode");
            //Slot logix_slot = program.Find("LogiX");
            if (currentSlot == null)
            {
                UniLog.Log("Cannot add " + nodeName + ", the provided program slot is null");
                return null;
            }
                
            Slot node_slot = currentSlot.AddSlot(nodeName);
            if (node_slot == null)
            {
                UniLog.Log("Could not add " + nodeName + ". Reason unknown.");
                return null;
            }

            return LogixFieldFullName(node_slot, nodeType);
        }

        void LogixProgramConnectInput(
            Slot program,
            LogixNode toNode, string inputSlotName,
            LogixNode fromNode, string outputSlotName)
        {
            LogixConnectInputTo(toNode, inputSlotName, fromNode, outputSlotName);
        }

        void LogixProgramConnectImpulse(
            Slot program,
            LogixNode toNode, string inputSlotName,
            LogixNode fromNode, string outputSlotName)
        {
            LogixConnectImpulse(fromNode, outputSlotName, toNode, inputSlotName);
        }

        void LogixSetPos(Slot program, LogixNode node, float x, float y)
        {
            node.Slot.LocalPosition = new float3(x, y, 0);
        }



        private void ParseProgram(string[] instruction)
        {
            if (instruction.Length < 3)
            {
                UniLog.Log("Not enough arguments for PROGRAM instruction");
                return;
            }
            foreach (string part in instruction)
            {
                UniLog.Log(part);
            }
            string programName       = ScriptConvertUserInput(instruction[1]);
            UniLog.Log("Program name " + programName);
            string fileFormatVersion = instruction[2];
            programSlot              = LogixProgramCreate(baseSlot, programName);
            programNodes             = new System.Collections.Generic.Dictionary<string, LogixNode>();
            programSlots             = new System.Collections.Generic.Dictionary<string, Slot>();
            programSlots.Add("S0", programSlot);
            currentSlot = logixSlot;
        }

        private void ParseNode(string[] instruction)
        {
            if (instruction.Length < 4)
            {
                UniLog.Log("Not enough arguments for NODE instruction");
                return;
            }
            string id       = instruction[1];
            string nodeType = ScriptUnquoteString(instruction[2]);
            string nodeName = ScriptConvertUserInput(instruction[3]);
            LogixNode programNode =
                LogixProgramAddNode(currentSlot, nodeName, nodeType);
            UniLog.Log("Node added ? ");
            UniLog.Log(programNode != null);
            if (programNode != null)
            {
                programNodes?.Add(id, programNode);
            }
        }

        private void ParsePos(string[] instruction)
        {
            if (instruction.Length < 4)
            {
                UniLog.Log("Not enough arguments for POS instruction");
                return;
            }
            string nodeID = instruction[1];
            string posXStr = instruction[2];
            string posYStr = instruction[3];
            bool node_found =
                programNodes.TryGetValue(nodeID, out LogixNode node);
            if (!node_found)
            {
                UniLog.Log("[POS] Could not find node with id : " + nodeID);
                return;
            }

            float.TryParse(posXStr, out float posX);
            float.TryParse(posYStr, out float posY);
            LogixSetPos(currentSlot, node, posX / 1000.0f, - (posY / 1000.0f));
        }

        private void ParseConnection(string[] instruction)
        {
            if (instruction.Length < 5)
            {
                UniLog.Log("Not enough arguments for INPUT or IMPULSE instruction");
                return;
            }
            string toID           = instruction[1];
            string toInputName    = ScriptUnquoteString(instruction[2]);
            string fromID         = instruction[3];
            string fromOutputName = ScriptUnquoteString(instruction[4]);

            bool gotTo = programNodes.TryGetValue(toID, out LogixNode toNode);
            bool gotFrom = programNodes.TryGetValue(fromID, out LogixNode fromNode);

            if (!gotTo || !gotFrom)
            {
                UniLog.Log("Could not get the nodes with ID : " + toID + ", " + fromID);
                UniLog.Log("(" + gotTo + ", " + gotFrom + ")");
                return;
            }

            if (instruction[0] == "INPUT")
            {
                LogixProgramConnectInput(currentSlot, toNode, toInputName, fromNode, fromOutputName);
            }
            else
            {
                LogixProgramConnectImpulse(currentSlot, toNode, toInputName, fromNode, fromOutputName);
            }
        }

        private void ParseSetConst(string[] instruction)
        {
            if (instruction.Length < 3)
            {
                UniLog.Log("Not enough arguments for SETCONST instruction");
                return;
            }
            string nodeID = instruction[1];
            string value = ScriptConvertUserInput(instruction[2]);

            bool hasNode = programNodes.TryGetValue(nodeID, out LogixNode node);
            if (!hasNode)
            {
                UniLog.Log("Node " + nodeID + " not found !");
                return;
            }

            LogixSetConst(currentSlot, node, value);
        }

        void ParseComponent(string[] instruction)
        {
            if (instruction.Length < 2)
            {
                UniLog.Log("Not enough arguments for COMPONENT instruction");
                return;
            }

            string slotID = instruction[1];
            string componentTypeName = instruction[2];

            if (programSlots.TryGetValue(slotID, out Slot componentSlot) == false)
            {
                UniLog.Log($"Could not get slot {slotID}");
                return;
            }

            Type componentType = ScriptGetActualNodeType(componentTypeName);
            if (componentType == null)
            {
                UniLog.Log($"Could not resolve type {componentTypeName}");
                UniLog.Log("Not adding component");
                return;
            }

            componentSlot.AttachComponent(componentType);
        }

        public static object GetDefault(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var valueProperty = type.GetProperty("Value");
                type = valueProperty.PropertyType;
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        void ParseVar(string[] instruction)
        {
            if (instruction.Length < 4)
            {
                UniLog.Log("Not enough arguments for VAR instruction");
                return;
            }

            string slotID      = instruction[1];
            string varName     = ScriptConvertUserInput(instruction[2]);
            string varTypeName = ScriptUnquoteString(instruction[3]);

            if (programSlots.TryGetValue(slotID, out Slot varSlot) == false)
            {
                UniLog.Log($"Cannot get slot {slotID}");
                return;
            }

            UniLog.Log($"Adding to slot {varSlot.Name} - LGXID : {slotID}");

            Type varType = GetTypeFromFullName(varTypeName);

            if (varType == null)
            {
                UniLog.Log($"Could not retrieve type {varTypeName}");
                return;
            }

            UniLog.Log($"Got {varType.AssemblyQualifiedName} from {varTypeName}");

            DynamicVariableSpace dv = varSlot.GetComponent<DynamicVariableSpace>();
            if (dv == null)
            {
                dv = varSlot.AttachComponent<DynamicVariableSpace>();
                if (dv == null)
                {
                    UniLog.Log($"Could not generate component :C");
                    return;
                }
            }

            UniLog.Log("Added component");

            MethodInfo getManager = typeof(DynamicVariableSpace).GetMethod("GetManager").MakeGenericMethod(varType);

            if (getManager == null)
            {
                UniLog.Log($"Could not retrieve DynamicVariableSpace.GetManager<{varType}>");
                return;
            }

            UniLog.Log($"Got DynamicVariableSpace.GetManager<{varType}>");

            getManager.Invoke(dv, new object[] { "", false });

            MethodInfo createVariable = typeof(DynamicVariableHelper).GetMethod("CreateVariable");

            if (createVariable == null)
            {
                UniLog.Log($"Could not retrieve DynamicVariableHelper.CreateVariable");
                return;
            }

            MethodInfo createVariableSpec = createVariable.MakeGenericMethod(varType);

            if (createVariableSpec == null)
            {
                UniLog.Log($"Could not specialize CreateVariable with type {varType}");
                return;
            }

            UniLog.Log($"Got CreateVariable<{varType}>");
            UniLog.Log($"CreateVariable Arity : {createVariableSpec.Attributes}");
            createVariableSpec.Invoke(varSlot, new object[] { varSlot, varName, GetDefault(varType), true });

        }

        void ParseSlot(string[] instructions)
        {
            if (instructions.Length < 3)
            {
                UniLog.Log("Not enough arguments for SLOT");
                return;
            }

            string slotID   = instructions[1];
            string slotName = ScriptConvertUserInput(instructions[2]);

            if (programSlots.ContainsKey(slotID))
            {
                UniLog.Log($"Slot {slotID} already exists !");
                return;
            }

            /* FIXME Define the current slot with another instruction */
            Slot newSlot = logixSlot.AddSlot(slotName);
            newSlot.Tag = slotName;

            programSlots.Add(slotID, newSlot);
            currentSlot = newSlot;
        }

        void ParseSetNodeSlot(string[] instructions)
        {
            if (instructions.Length < 4)
            {
                UniLog.Log("Not enough arguments for SETNODESLOT");
                return;
            }

            string nodeID = instructions[1];
            string nodeInputName = ScriptUnquoteString(instructions[2]);
            string slotID = instructions[3];

            if (programNodes.TryGetValue(nodeID, out LogixNode node) == false)
            {
                UniLog.Log("Node " + nodeID + " not found !");
                return;
            }

            UniLog.Log("[SETNODESLOT] Got node");

            if (programSlots.TryGetValue(slotID, out Slot slotToConnect) == false)
            {
                UniLog.Log($"Slot {slotID} not found !");
                return;
            }

            UniLog.Log("[SETNODESLOT] Got slot");

            ReferenceNode<Slot> refNode = slotToConnect.GetComponent<ReferenceNode<Slot>>();
            if (refNode == null)
            {
                refNode = slotToConnect.AttachComponent<ReferenceNode<Slot>>();
                if (refNode == null)
                {
                    UniLog.Log("[SETNODESLOT] Could not create the reference node");
                    return;
                }
                if (refNode.RefTarget.TrySet(slotToConnect) == false)
                {
                    UniLog.Log("[SETNODESLOT] Could not setup the reference node");
                    refNode.Destroy();
                    return;
                }
            }

            /* FIXME Check if the type is compatible before casting... */
            var input = node.TryGetField(nodeInputName);

            if (input == null)
            {
                UniLog.Log($"Input {nodeInputName} not found on {node.GetType()}");
                return;
            }

            UniLog.Log("[SETNODESLOT] Got Input");

            bool connected = ((ISyncRef)input).TrySet(refNode);
            UniLog.Log($"[SETNODESLOT] Connected ? {connected}");

        }

        private void ScriptParseLine(string line)
        {
            UniLog.Log("Parsing :");
            UniLog.Log(line);
            string[] instruction = line.Split(scriptFieldSeparator);
            switch (instruction[0])
            {
                case "PROGRAM":
                    {
                        ParseProgram(instruction);
                    }
                    break;
                case "NODE":
                    {
                        ParseNode(instruction);
                    }
                    break;
                case "POS":
                    {
                        ParsePos(instruction);
                    }
                    break;
                case "INPUT":
                case "IMPULSE":
                    {
                        ParseConnection(instruction);
                    }
                    break;
                case "SETCONST":
                    {
                        ParseSetConst(instruction);
                    }
                    break;
                case "COMPONENT":
                    {
                        ParseComponent(instruction);
                    }
                    break;
                case "VAR":
                    {
                        ParseVar(instruction);
                    }
                    break;
                case "SLOT":
                    {
                        ParseSlot(instruction);
                    }
                    break;
                case "SETNODESLOT":
                    {
                        ParseSetNodeSlot(instruction);
                    }
                    break;

            }
        }


        protected override void OnCommonUpdate()
        {
            base.OnCommonUpdate();
            if (WebsocketHasNewData())
            {
                UniLog.Log("Got new data !");
                string toParse = remainsFromWS + WebsocketNewData();
                UniLog.Log(toParse);
                string[] scriptLines = toParse.Split(new[] { '\n' });
                /* The last one is always considered as an incomplete line
                 * and will be affected to remainsFromWS */
                int linesToParseCount = scriptLines.Length - 1;
                for (int i = 0; i < linesToParseCount; i++)
                {
                    string line = scriptLines[i];
                    UniLog.Log("Parsing : " + line);
                    ScriptParseLine(line);
                }
                remainsFromWS = scriptLines[linesToParseCount];
            }
        }

        void MyyWebsocketError(WebsocketClient client, string error)
        {
            UniLog.Log("[VoyagePlugin] Websocket error with client : ");
            UniLog.Log(error);
        }

        void MyyWebsocketClosed(WebsocketClient client)
        {
            UniLog.Log("[VoyagePlugin] Websocket closed");
        }

        void MyyWebsocketConnected(WebsocketClient client)
        {
            UniLog.Log("[VoyagePlugin] Websocket connected");
            baseSlot = client.Slot;
            UniLog.Log("Base slot is now : ");
            UniLog.Log(baseSlot);
        }

        void MyyWebsocketMessageBinary(WebsocketClient client, byte[] data)
        {
            UniLog.Log("[VoyagePlugin] Websocket binary message");
            UniLog.Log(data);
        }

        void MyyWebsocketMessageText(WebsocketClient client, string text)
        {
            UniLog.Log("[VoyagePlugin] Websocket text message");
            UniLog.Log(text);
            WebsocketSaveMessage(text);
        }

        /* https://stackoverflow.com/questions/457676/check-if-a-class-is-derived-from-a-generic-class */
        static Type IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return toCheck;
                }
                toCheck = toCheck.BaseType;
            }
            return null;
        }

        string ScriptUnquoteString(string singleQuoted)
        {
            return singleQuoted.Trim(new[] { '\'', ' ' }).Replace("''", "'");
        }

        /* https://stackoverflow.com/questions/11743160/how-do-i-encode-and-decode-a-base64-string */
        string ScriptConvertUserInput(string userInputBase64)
        {
            /* Trim eventual spaces and quote characters */
            string unquotedBase64 = userInputBase64.Trim(new[] { '"', ' ' });
            string decodedString = "";
            try
            {
                var decodedBytes = System.Convert.FromBase64String(unquotedBase64);
                decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);
            }
            catch (Exception e)
            {
                UniLog.Log(
                    "Converting the following base64 input to String provoked an Exception.\n" +
                    "Base64 string : " + userInputBase64 + "\n" +
                    "Exception : " + e.Message);
            }

            UniLog.Log("Decoded : " + userInputBase64 + " to " + decodedString);

            return decodedString;
        }

        void LogixSetConst(Slot program, LogixNode node, string inputValue)
        {
            Type nodeType = node.GetType();
            string nodeTypeStr = nodeType.FullName;

            switch(nodeTypeStr)
            {
                case "FrooxEngine.LogiX.Input.BoolInput":
                    {
                        BoolInput inputNode = (BoolInput)node;
                        if (RobustParser.TryParse(inputValue, out bool value))
                        {
                            inputNode.Value.Value = value;
                        }
                        else
                        {
                            UniLog.Log("Could not convert " + inputValue + " to bool");
                            return;
                        }
                    }
                    break;
                case "FrooxEngine.LogiX.Input.Bool2Input":
                    {
                        Bool2Input inputNode = (Bool2Input)node;
                        if (RobustParser.TryParse(inputValue, out bool2 value))
                        {
                            inputNode.Value.Value = value;
                        }
                        else
                        {
                            UniLog.Log("Could not convert " + inputValue + " to bool");
                            return;
                        }
                    }
                    break;
                case "FrooxEngine.LogiX.Input.Bool3Input":
                    {
                        Bool3Input inputNode = (Bool3Input)node;
                        if (RobustParser.TryParse(inputValue, out bool3 value))
                        {
                            inputNode.Value.Value = value;
                        }
                        else
                        {
                            UniLog.Log("Could not convert " + inputValue + " to bool");
                            return;
                        }
                    }
                    break;
                case "FrooxEngine.LogiX.Input.Bool4Input":
                    {
                        Bool4Input inputNode = (Bool4Input)node;
                        if (RobustParser.TryParse(inputValue, out bool4 value))
                        {
                            inputNode.Value.Value = value;
                        }
                        else
                        {
                            UniLog.Log("Could not convert " + inputValue + " to bool");
                            return;
                        }
                    }
                    break;
                case "FrooxEngine.Logix.Input.ColorInput":
                    {
                        
                        ColorTextInput colorInputNode = (ColorTextInput)node;
                        if (RobustParser.TryParse(inputValue, out color value))
                        {
                            colorInputNode.CurrentValue = value;
                        }
                        else
                        {
                            UniLog.Log("Could not convert " + inputValue + " to Color");
                            return;
                        }
                    }
                    break;
                default:
                    Type valueTextFieldType =
                        IsSubclassOfRawGeneric(typeof(ValueTextFieldNodeBase<>), nodeType);
                    
                    if (valueTextFieldType == null)
                    {
                        UniLog.Log(
                            "[BUG] Don't know how to deal with input of nodes of type : \n" +
                            nodeTypeStr);
                        return;
                    }

                    LogixSetConstValueText(program, node, inputValue, valueTextFieldType);
                    break;
            }
        }

        void LogixSetConstValueText(Slot program, LogixNode node, string inputValue, Type valueTextFieldType)
        {

            UniLog.Log("Setting up the value through reflection");
            Type[] genericArgs = valueTextFieldType.GetGenericArguments();
            /* FIXME This is a weird check... Verify that this issue can really happen */
            if (genericArgs.Length == 0)
            {
                UniLog.Log(
                    "[BUG] Cannot get the right generic type to parse the provided values for :\n" +
                    valueTextFieldType.FullName);
                return;
            }

            Type valueType = genericArgs[0];
            System.Reflection.MethodInfo parser = typeof(RobustParser).GetMethod(
                nameof(RobustParser.TryParse),
                new[] { typeof(string), valueType.MakeByRefType() });
            
            if (parser == null)
            {
                UniLog.Log(
                    "[BUG] Could not get a parser for values of type " + valueType.FullName + "\n" +
                    "For setting the value of a : " + node.GetType().FullName);
                return;
            }

            PropertyInfo currentValueProp = valueTextFieldType.GetProperty("CurrentValue");

            if (currentValueProp == null)
            {
                UniLog.Log(
                    "[BUG] Could not get the property 'CurrentValue' from node of type :\n" +
                    node.GetType().FullName + "\n" +
                    "This is the only way known to setup the value of such fields...");
                return;
            }

            var parameters = new object[] { inputValue, null };
            if ((bool)parser.Invoke(null, parameters) == false)
            {
                UniLog.Log("Could not convert '" + inputValue + "' to " + genericArgs[0].Name);
                UniLog.Log("(Base64 version : " + inputValue + ")");
                return;
            }

            object parsedValue = parameters[1];
            currentValueProp.SetValue(node, parsedValue);
            UniLog.Log("Parsed value : " + parsedValue.ToString() + " (from : " + inputValue + ")");
        }

        Type GetTypeFromFullName(string typeTFullName)
        {
            string assemblyFullName = GetAssemblyNameFrom(typeTFullName);

            if (assemblyFullName == "")
            {
                UniLog.Log("Cannot get the assembly full name of : " + typeTFullName);
                return null;
            }
            string typeTSearchName = typeTFullName + ", " + assemblyFullName;
            Type returnedType = Type.GetType(typeTSearchName);
            if (returnedType == null)
            {
                UniLog.Log("Cannot find type : " + typeTSearchName);
            }
            return returnedType;
        }

        Type ScriptGetActualNodeType(string providedNodeTypeFullName)
        {
            int bracketPosition = providedNodeTypeFullName.IndexOf('<');
            if (bracketPosition < 0)
            {
                return LogixGetType(providedNodeTypeFullName);
            }
            else /* Generic types, yay... */
            {
                int endBracketPosition = providedNodeTypeFullName.IndexOf('>');
                if (endBracketPosition == -1)
                {
                    UniLog.Log(
                        "Missing closing bracket for generic type definition :\n" +
                        providedNodeTypeFullName);
                    return null;
                }

                /* Basically, we'll receive something like :
                 * FrooxEngine.LogiX.Operators.NotNull<FrooxEngine-Slot>
                 * or
                 * FrooxEngine.Logix.Data.ReadDynamicVariable<System-Single>
                 */
                string genericTypeName = providedNodeTypeFullName.Substring(0, bracketPosition);

                int afterStartBracketIdx = bracketPosition + 1;
                int betweenBracketsLength =
                    endBracketPosition - afterStartBracketIdx;
                string specialTypeNames = 
                    providedNodeTypeFullName.Substring(afterStartBracketIdx, betweenBracketsLength);

                string[] typeTNamesList = specialTypeNames.Split(new[] { ',' });
                Type[] specialTypes = new Type[typeTNamesList.Length];
                int typesCount = typeTNamesList.Length;

                /* It seems that all generics type names are suffixed with '`' followed
                 * by the amount of generic arguments */
                genericTypeName += "`" + typesCount;
                Type genericType = LogixGetType(genericTypeName);
                if (genericType == null)
                {
                    UniLog.Log("Could not find generic type " + genericTypeName);
                    return null;
                }

                for (int i = 0; i < typesCount; i++)
                {
                    Type specialType = GetTypeFromFullName(typeTNamesList[i].Trim());

                    if (specialType == null)
                    {
                        UniLog.Log("For : " + providedNodeTypeFullName);
                        return null;
                    }
                    specialTypes[i] = specialType;
                }

                Type specializedType = genericType.MakeGenericType(specialTypes);
                if (specializedType == null)
                {
                    UniLog.Log(
                        "Could not specialize " + specializedType.FullName +
                        " with the following types :");
                    foreach (Type t in specialTypes)
                    {
                        UniLog.Log(t.FullName);
                    }
                    UniLog.Log("For : " + providedNodeTypeFullName);
                }

                /* Could be null if the type was not found */
                return specializedType;
            }
        }

        string GetAssemblyNameFrom(string typeFullName)
        {
            string nmspc = typeFullName.Split(new[] { '.' })[0];
            switch (nmspc)
            {
                case "System":
                    {
                        return systemAssemblyName;
                    }
                case "FrooxEngine":
                    {
                        return frooxAssemblyName;
                    }
                case "BaseX":
                    {
                        return basexAssemblyName;
                    }
                case "CodeX":
                    {
                        return codexAssemblyName;
                    }
                case "CloudX":
                    {
                        return cloudxAssemblyName;
                    }
                default:
                    {
                        UniLog.Log("Cannot find an assemby name for : " + typeFullName);
                        UniLog.Log("(Splitted version : " + nmspc + ")");
                        return "";
                    }
            }
        }

        protected override void OnChanges()
        {
            if (connect)
            {
                WebsocketSlotAdd(Slot, websocketServerAddress);
            }
            else
            {
                WebsocketSlotRemove();
            }
        }



    }
}
