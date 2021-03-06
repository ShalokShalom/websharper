// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2016 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper.Sitelets.Tests

open System
open WebSharper

module Client =
    open WebSharper.JavaScript

    [<JavaScript>]
    type Node =
        | Elt of string * Node[]
        | Text of string

        member this.ToNode() =
            match this with
            | Text t -> JS.Document.CreateTextNode(t) :> Dom.Node
            | Elt (n, ch) ->
                let e = JS.Document.CreateElement(n)
                for ch in ch do e.AppendChild(ch.ToNode()) |> ignore
                e :> Dom.Node

        interface IControlBody with
            member this.ReplaceInDom x =
                x.ParentNode.ReplaceChild(this.ToNode(), x) |> ignore

    [<JavaScript>]
    let Elt n ([<ParamArray>] ch) = Node.Elt(n, ch)

    [<JavaScript>]
    let Text t = Node.Text(t)

    [<Sealed>]
    type SignupSequenceControl() =
        inherit Web.Control()

        [<JavaScript>]
        override this.Body =
            Elt "div" [|Text "SIGNUP-SEQUENCE"|] :> _

    [<Sealed>]
    type LoginControl(link: string) =
        inherit Web.Control()

        [<JavaScript>]
        override this.Body =
            Elt "div" [|Text ("LOGIN: " + link)|] :> _

    [<JavaScript>]
    let Widget () =
        Elt "button" [|Text "click me!"|]

/// A mini server-side HTML language
module Server =
    open WebSharper.Web

    [<AbstractClass>]
    type RequiresNoResources() =
        interface IRequiresResources with
            member this.Requires(_) = Seq.empty
            member this.Encode(_, _) = []

    type Elt(name, [<System.ParamArray>] contents: INode[]) =
        let attributes, children =
            contents |> Array.partition (fun n -> n.IsAttribute)
        interface IRequiresResources with
            member this.Requires(meta) = children |> Seq.collect (fun c -> c.Requires(meta))
            member this.Encode(meta, json) =  children |> Seq.collect (fun c -> c.Encode(meta, json)) |> List.ofSeq
        interface INode with
            member this.Write(ctx, w) =
                w.WriteBeginTag(name)
                attributes |> Array.iter (fun n -> n.Write(ctx, w))
                if Array.isEmpty children && WebSharper.Core.Resources.HtmlTextWriter.IsSelfClosingTag(name) then
                    w.Write(WebSharper.Core.Resources.HtmlTextWriter.SelfClosingTagEnd)
                else
                    w.Write(WebSharper.Core.Resources.HtmlTextWriter.TagRightChar)
                    children |> Array.iter (fun n -> n.Write(ctx, w))
                    w.WriteEndTag(name)
            member this.IsAttribute = false

    type Attr(name, value) =
        inherit RequiresNoResources()
        interface INode with
            member this.Write(ctx, w) =
                w.WriteAttribute(name, value)
            member this.IsAttribute = true

    type Text(txt) =
        inherit RequiresNoResources()
        interface INode with
            member this.Write(ctx, w) =
                w.WriteEncodedText(txt)
            member this.IsAttribute = false

/// The website definition.
module SampleSite =
    open WebSharper.Web
    open WebSharper.Sitelets
    open Server

    /// Actions that corresponds to the different pages in the site.
    type Action =
        | Home
        | Contact
        | Protected
        | Login of option<Action>
        | [<Query("firstName", "lastName", "message")>] FormResultGet of firstName: string * lastName: string * message: string
        | [<FormData("firstName", "lastName", "message")>] FormResultPost of firstName: string * lastName: string * message: string
        | Logout
        | Echo of string
        | Api of Api.Action
        | [<EndPoint "GET /test.png">] TestImage
        | [<Method "POST">] Json of ParseRequestResult<Json.Action>
        | [<EndPoint "/"; Wildcard>] AnythingElse of string

    /// A helper function to create a hyperlink
    let private ( => ) title href =
        Elt("a", Attr("style", "padding-right:5px"), Attr("href", href), Text title)

    /// A helper function to create a 'fresh' url with a random get parameter
    /// in order to make sure that browsers don't show a cached version.
    let private RandomizeUrl url =
        url + "?d=" + System.Uri.EscapeUriString (System.DateTime.Now.ToString())

    /// User-defined widgets.
    module Widgets =

        /// Widget for displaying login status or a link to login.
        let LoginInfo (ctx: Context<Action>) =
            async {
                let! user = ctx.UserSession.GetLoggedInUser ()
                return [
                    (
                        match user with
                        | Some email ->
                            "Log Out (" + email + ")" =>
                                (RandomizeUrl <| ctx.Link Action.Logout)
                        | None ->
                            "Login" => (ctx.Link <| Action.Login None)
                    )
                ]
            }

    type Template =
        {
            Title: string
            Body: seq<Web.INode>
            Menu: seq<Web.INode>
            Login: seq<Web.INode>
        }

    let Tpl (t: Async<Template>) =
        async {
            let! t = t
            return! Content.Page(
                Title = t.Title,
                Body = [
                    yield! t.Login
                    yield! t.Menu
                    yield! t.Body
                    yield ClientSide <@ Client.Widget () @> :> _
                ]
            )
        }

    /// A template function that renders a page with a menu bar, based on the `Skin` template.
    let Template title main (ctx: Context<Action>) =
        Tpl <|
            let menu =
                let ( ! ) x = ctx.Link x
                [
                        "Home" => !Action.Home
                        "Contact" => !Action.Contact
                        "Say Hello" => !(Action.Echo "Hello")
                        "Protected" => (RandomizeUrl <| !Action.Protected)
                ]
                |> List.map (fun link ->
                    Elt("label", Attr("class", "menu-item"), link)
                )
            async {
                let! login = Widgets.LoginInfo ctx
                return {
                    Title = title
                    Menu = Seq.cast menu
                    Login = Seq.cast login
                    Body = main ctx
                }
            }

    /// The pages of this website.
    module Pages =

        /// The home page.
        let HomePage =
            Template "Home" <| fun ctx ->
                [
                    Elt("h1", Text "Welcome to our site!")
                    "Let us know how we can contact you" => ctx.Link Action.Contact
                    Elt("div", ClientSide <@ Client.Elt "b" [|Client.Text "It's working baby"|] @>)
                    Elt("div",
                        ClientSide
                            <@ Client.Elt "i" [|
                                Client.Text "It "
                                Client.Elt "b" [|Client.Text "really"|]
                                Client.Text " is!"
                            |] @>)
                ]

        /// A page to collect contact information.
        let ContactPage =
            let form isGet =
                let method = if isGet then "get" else "post" 
                let action = "/sitelet-tests/FormResult" + if isGet then "Get" else "Post"  
                Elt("div",
                    Elt("form", Attr("action", action), Attr("method", method),
                        Text("First name: "),
                        Elt("input", Attr("type", "text"), Attr("name", "firstName")),
                        Text("Last name: "),
                        Elt("input", Attr("type", "text"), Attr("name", "lastName")),
                        Elt("textarea", Attr("name", "message")),
                        Elt("input", Attr("type", "submit"), Attr("value", "Submit with " + method))
                    )
                )
            Template "Contact" <| fun ctx ->
                [
                    Elt("h1", Text "Contact Form")
                    Elt("div", new Client.SignupSequenceControl())

                    form true
                    form false
                ]

        /// A simple page that echoes a parameter.
        let EchoPage param =
            Template "Echo" <| fun ctx ->
                [
                    Elt("h1", Text param)
                ]

        /// A simple page that echoes two form fields.
        let FormResult x y z =
            Template "Form result" <| fun ctx ->
                [
                    Elt("h1", Text (x + " " + y))
                    Elt("code", Text z)
                ]
       
        /// A simple login page.
        let LoginPage (redirectAction: option<Action>) =
            Template "Login" <| fun ctx ->
                let redirectLink =
                    match redirectAction with
                    | Some action -> action
                    | None -> Action.Home
                    |> ctx.Link
                [
                    Elt("h1", Text "Login")
                    Elt("p",
                        Text "Login with any username and password='",
                        Elt("i", Text "password"),
                        Text "'."
                    )
                    new Client.LoginControl(redirectLink)
                ]

        /// A simple page that users must log in to view.
        let ProtectedPage =
            Template "Protected" <| fun ctx ->
                [
                    Elt("h1", Text "This is protected content - thanks for logging in!")
                ]

    /// The sitelet that corresponds to the entire site.
    let EntireSite =
        // A simple sitelet for the home page, available at the root of the application.
        let home =
            Sitelet.Content "/" Action.Home Pages.HomePage

        // An automatically inferred sitelet created for the basic parts of the application.
        let basic =
            Sitelet.Infer <| fun ctx action ->
                match action with
                | Action.Contact ->
                    Pages.ContactPage ctx
                | Action.Echo param ->
                    Pages.EchoPage param ctx
                | Action.Login action->
                    Pages.LoginPage action ctx
                | Action.Logout ->
                    // Logout user and redirect to home
                    async {
                        do! ctx.UserSession.Logout ()
                        return! Content.RedirectTemporary Action.Home
                    }
                | Action.FormResultGet (x, y, z)
                | Action.FormResultPost (x, y, z) ->
                    Pages.FormResult x y z ctx
                | Action.Home ->
                    Content.RedirectPermanent Action.Home
                | Action.Protected ->
                    Content.ServerError
                | Action.Api _ ->
                    Content.ServerError
                | Action.Json (ParseRequestResult.Success a) ->
                    WebSharper.Sitelets.Tests.Json.Content a
                | Action.Json err ->
                    Content.Json err
                    |> Content.SetStatus Http.Status.NotFound
                | Action.TestImage ->
                    Content.File "~/image.png"
                    |> Content.WithContentType "image/png"
                | Action.AnythingElse p ->
                    Content.Text ("Unmatched path: " + p)
                    |> Content.SetStatus Http.Status.NotFound

        // A sitelet for the protected content that requires users to log in first.
        let authenticated =
            let filter : Sitelet.Filter<Action> =
                {
                    VerifyUser = fun _ -> true
                    LoginRedirect = Some >> Action.Login
                }

            Sitelet.Protect filter <|
                Sitelet.Content "/protected" Action.Protected Pages.ProtectedPage

        // A sitelet wrapping the API sitelet into the main action.
        let api = Sitelet.EmbedInUnion <@ Action.Api @> Api.Sitelet

        // Compose the above sitelets into a larger one.
        [
            home
            authenticated
            Sitelet.Shift "api" api
            basic
        ]
        |> Sitelet.Sum
