// This file is part of EoS
// Copyright (c) 2009-2025 Thomas Chust
//               2009-2017 Bayerisches Geoinstitut, Bayreuth
//               2009-2017 Ludwig-Maximilians-Universität, München
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#nowarn "0025"

namespace EoS.Slackbot
open System
open System.Runtime.Serialization

/// Slack message attachment.
[<DataContract>]
type Attachment =
  { [<field: DataMember(Name = "title")>]
    Title : string
    [<field: DataMember(Name = "text")>]
    Text : string
    [<field: DataMember(Name = "color")>]
    Color : string }

/// Slack message.
[<DataContract>]
type Message =
  { [<field: DataMember(Name = "response_type")>]
    ResponseType : string
    [<field: DataMember(Name = "text")>]
    Text : string
    [<field: DataMember(Name = "attachments")>]
    Attachments : Collections.Generic.List<Attachment> }

/// Slack message extension methods.
[<AutoOpen>]
module MessageExtensions =
  let private MetaRegex =
    Text.RegularExpressions.Regex(@"[&<>]")

  let private EscapeRegex =
    Text.RegularExpressions.Regex(@"&(amp|lt|gt);")

  type Message with
    /// Escape plain message text for Slack.
    static member EscapeText(text) =
      MetaRegex.Replace(
        text, Text.RegularExpressions.MatchEvaluator(fun it ->
          "&" + (match it.Value with
                 | "&" -> "amp"
                 | "<" -> "lt"
                 | ">" -> "gt") + ";"))

    /// Unescape message text from Slack.
    static member UnescapeText(text) =
      EscapeRegex.Replace(
        text, Text.RegularExpressions.MatchEvaluator(fun it ->
          match it.Groups[1].Value with
          | "amp" -> "&"
          | "lt"  -> "<"
          | "gt"  -> ">"))

    /// Create a basic in-channel message.
    static member Channel(text) =
      { ResponseType = "in_channel"
        Text = Message.EscapeText(text)
        Attachments = null }

    /// Create a basic error message.
    static member Error(text) =
      { ResponseType = "ephemeral"
        Text = null
        Attachments =
          Collections.Generic.List(
            [{ Title = "Error"
               Text = Message.EscapeText(text)
               Color = "danger" }]) }

/// Slack slash command notification.
type CommandEventArgs(Text : string, ResponseUrl : string) =
  inherit EventArgs()

  /// The text contained in the command.
  member this.Text = Text

  /// The URL for asynchronous responses to the command.
  member this.ResponseUrl = ResponseUrl

  /// The response message to send for the command.
  member val Response : Option<Message> = None with get, set

/// Slack slash command handler delegate type.
type CommandEventHandler = EventHandler<CommandEventArgs>

/// Exception indicating that a Slack event could not be handled and
/// suggesting a suitable HTTP status code.
exception SlackEventException of Net.HttpStatusCode

/// Helper class that turns HTTP requests from Slack into events.
type SlackEventDispatcher() =
  [<Literal>]
  static let Ampersand = 38

  [<Literal>]
  static let EqualSign = 61

  [<Literal>]
  static let PlusSign = 43

  [<Literal>]
  static let EndOfData = -1

  static let flushString (buffer : Text.StringBuilder) =
    let v = buffer.ToString()
    buffer.Clear() |> ignore
    v

  static let parseFormData (request : Net.HttpListenerRequest) =
    let qry = Collections.Specialized.NameValueCollection(request.QueryString)
    let buf = Text.StringBuilder()
    use inp = new IO.StreamReader(request.InputStream, request.ContentEncoding)
    let rec loop k =
      match inp.Read() with
      | EqualSign when isNull k ->
        loop (flushString buf)
      | Ampersand ->
        qry.Add(k, flushString buf |> Uri.UnescapeDataString)
        loop null
      | EndOfData ->
        if not (isNull k) || buf.Length > 0 then
          qry.Add(k, flushString buf |> Uri.UnescapeDataString)
      | PlusSign when not (isNull k) ->
        buf.Append(' ') |> ignore
        loop k
      | c ->
        buf.Append(char c) |> ignore
        loop k
    loop null
    qry

  let commandEvent = Event<CommandEventHandler, _>()

  /// Event triggered by incoming slash commands.
  [<CLIEvent>]
  member this.CommandEvent = commandEvent.Publish

  /// Tokens to check for in incoming requests.
  member val Tokens : Set<String> = Set.empty with get, set

  /// Handles an HTTP request.
  member this.Handle(context : Net.HttpListenerContext) =
    let req = context.Request
    use rsp = context.Response

    try
      if req.HttpMethod = "POST" && not req.HasEntityBody then
        raise (SlackEventException Net.HttpStatusCode.BadRequest)

      let data = parseFormData req
      if not (Set.contains data["token"] this.Tokens) then
        raise (SlackEventException Net.HttpStatusCode.Unauthorized)

      if data["ssl_check"] = "1" then
        raise (SlackEventException Net.HttpStatusCode.OK)

      let evt = CommandEventArgs(data["text"], data["response_url"])
      commandEvent.Trigger(this, evt)

      match evt.Response with
      | Some msg ->
        let ser = Json.DataContractJsonSerializer(typeof<Message>)
        rsp.ContentType <- "application/json"
        ser.WriteObject(rsp.OutputStream, msg)
      | None ->
        raise (SlackEventException Net.HttpStatusCode.OK)

    with
    | SlackEventException status ->
      rsp.StatusCode <- int status

    rsp.Close()

  /// Runs a synchronous event loop handling requests from the given
  /// HttpListener.
  member this.Run(listener : Net.HttpListener) =
    try
      while listener.IsListening do
        listener.GetContext()
        |> this.Handle
    with
    | :? Net.HttpListenerException when not listener.IsListening ->
      ()
