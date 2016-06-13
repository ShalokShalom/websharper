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

namespace WebSharper.Sitelets

open System.Collections.Generic

type Context<'Action>
    (
        ApplicationPath : string,
        Link : 'Action -> string,
        Json : WebSharper.Core.Json.Provider,
        Metadata : WebSharper.Core.Metadata.Info,
        ResolveUrl : string -> string,
        ResourceContext : WebSharper.Core.Resources.Context,
        Request : Http.Request,
        RootFolder : string,
        UserSession : WebSharper.Web.IUserSession,
        Environment : IDictionary<string, obj>
    ) =

    interface WebSharper.Web.IContext with
        member this.RequestUri = Request.Uri
        member this.RootFolder = RootFolder
        member this.UserSession = UserSession
        member this.Environment = Environment

    member this.ApplicationPath = ApplicationPath
    member this.Link(e) = Link e
    member this.Json = Json
    member this.Metadata = Metadata
    member this.ResolveUrl p = ResolveUrl p
    member this.ResourceContext = ResourceContext
    member this.Request = Request
    member this.RootFolder = RootFolder
    member this.UserSession = UserSession
    member this.Environment = Environment

type Context(ctx: Context<obj>) =
    inherit Context<obj>(ctx.ApplicationPath, ctx.Link, ctx.Json, ctx.Metadata, ctx.ResolveUrl,
        ctx.ResourceContext, ctx.Request, ctx.RootFolder, ctx.UserSession, ctx.Environment)

    static member Map (f: 'T2 -> 'T1) (ctx: Context<'T1>) : Context<'T2> =
        Context<'T2>(
            ApplicationPath = ctx.ApplicationPath,
            Link = (ctx.Link << f),
            Json = ctx.Json,
            Metadata = ctx.Metadata,
            ResolveUrl = ctx.ResolveUrl,
            ResourceContext = ctx.ResourceContext,
            Request = ctx.Request,
            RootFolder = ctx.RootFolder,
            UserSession = ctx.UserSession,
            Environment = ctx.Environment
        )
