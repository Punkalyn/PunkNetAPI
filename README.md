General guide:

# PunkNet.RegisterPunkHandler(IMessageHandler handler)
Allows you to register a handler from PunkNetAPI to your own mod, so that your mod can listen for PunkNetMessages. Build a (Class : IMessageHandler) that contains a PunkHandler(PunkNetMessage) method, and that method will be called when a PunkNetMessage is detected. Or at least it should.

Be sure to filter the method to ensure you're only retrieving relevant information for your method.

# ToServerMessage(string modSource, Dictionary<string, object> data)
A client -> server message, which contains the sending player's netId, should be given your mod's name as a string, and then passes a dictionary of <string, object> which is your mod's payload. The string key is whatever identifier you want for that piece of data, and object is the data itself. If given a string, it will process a string. If given an int, it will process an int. I've covered a lot of basic types, but if your type is niche or edge case, please let me know so I can look into implementing it.

# ToClientsMessage(string modSource, Dictionary<string, object> data)
A server -> clients message, similar to ToServerMessage. However, there is no native filtering for this, so any targeting or filtering must be done with a combination of 'data' and your receiving handlers.

# ToTargetMessage(uint targetNetId, PunkNetMessage message)
An experimental method to allow sending a message only to a specific client, useful for things like letting a player know what version of your mod is running on a server, but with slightly higher overhead that ToClientsMessage in most cases.

# SingleToServerMessage(string modSource, string customKey, object customValue)
# SingleToClientsMessage(string modSource, string customKey, object customValue)
For those times that you -don't- want to build an entire dictionary, because you're sending only one bit of information (a single string, int, etc). Exactly same functionality as ToServerMessage and ToClientsMessage, but it builds a single entry dictionary for you.
