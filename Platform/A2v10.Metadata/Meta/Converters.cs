// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using Newtonsoft.Json;

namespace A2v10.Metadata;

public class CommandBarItemConverter : JsonConverter<CommandBarItem>
{
    private const String SepToken = "$sep";

    public override void WriteJson(JsonWriter writer, CommandBarItem value, JsonSerializer serializer)
    {
        String token = value.Kind switch
        {
            CommandBarItemKind.Command => value.Command!.Value.ToString(),
            CommandBarItemKind.Separator => SepToken,
            _ => throw new JsonSerializationException($"Unknown ToolbarItemKind: {value.Kind}")
        };
        writer.WriteValue(token);
    }

    public override CommandBarItem ReadJson(JsonReader reader, Type objectType,
        CommandBarItem existingValue, Boolean hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException(
                $"Expected string for ToolbarItem, got {reader.TokenType}");

        String token = (String)reader.Value!;

        return token switch
        {
            SepToken => CommandBarItem.Separator,
            _ => Enum.Parse<EntityCommandType>(token)
        };
    }
}