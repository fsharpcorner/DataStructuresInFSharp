// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
open Microsoft.FSharp.Control.WebExtensions 
open System.Net
open FSharp.Data
open System.IO

let fetchUrl (url:string) = 
    async {
        try 
            let req = WebRequest.Create(url) :?> HttpWebRequest
            req.UserAgent <- "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246";
            req.Method <- "GET";
            req.AllowAutoRedirect <- true;
            req.MaximumAutomaticRedirections <- 4;
            let! response1 = req.AsyncGetResponse()
            let response = response1 :?> HttpWebResponse
            use stream = response.GetResponseStream()
            use streamreader = new System.IO.StreamReader(stream)
            return! streamreader.AsyncReadToEnd() // .ReadToEnd()
         with
             _ -> return "" // if there's any exception, just return an empty string
    }

let getImageUrls (page:string) = 
    //let start = "<span class=\"photo_container pc_t\"><a href=\""
    let start = "background-image: url("
    let slen = start.Length
    let getUrl idx = 
        let idx2 = page.IndexOf('"', idx + slen)
        page.Substring(idx+slen, idx2-idx-slen)

    let rec scan (pos:int) = 
        seq {
            let idx = page.IndexOf(start, pos)
            if idx <> -1 then
                let url = getUrl idx
                //yield ("http://www.flickr.com/" + url)
                yield "http://" + url.Substring(2, url.Length-3)
                yield! scan (idx+1)
        }
    scan 0

let getImage (imageUrl:string) =
    async {
       try 
            let req = WebRequest.Create(imageUrl) :?> HttpWebRequest
            req.UserAgent <- "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246";
            req.Method <- "GET";
            req.AllowAutoRedirect <- true;
            req.MaximumAutomaticRedirections <- 4;
            let! response1 = req.AsyncGetResponse()
            let response = response1 :?> HttpWebResponse
            use stream = response.GetResponseStream()
            let ms = new MemoryStream()
            let bytesRead = ref 1
            let buffer = Array.create 0x1000 0uy
            while !bytesRead > 0 do
                bytesRead := stream.Read(buffer, 0, buffer.Length)
                ms.Write(buffer, 0, !bytesRead)
            return ms.ToArray();

        with
            _ -> return Array.create 0 0uy // if there's any exception, just return an empty image
    }

let getBetween (page:string) (head:string) = 
    let len = head.Length
    let idx = page.IndexOf(head)
    let idx2 = page.IndexOf('"', idx+len)
    let between = page.Substring(idx+len, idx2 - idx - len)
    between

let getImageUrlAndTags (page:string) = 
    let header = "class=\"photoImgDiv\">"
    let idx = page.IndexOf(header)
    let url = getBetween (page.Substring(idx)) "<img src=\""
 
    let header2 = "<meta name=\"keywords\" content=\""
    let tagStr = getBetween page header2
 
    let s = tagStr.Split([|','|], System.StringSplitOptions.RemoveEmptyEntries)
    let tags = s |> Array.map (fun t -> t.Trim())
    url, tags


let getImagesWithTag (tag:string) (pages:int) = 
    let rooturl = @"http://www.flickr.com/search/?q="+tag+"&m=tags&s=int"
    seq {
        for i=1 to pages do 
            let url = rooturl + "&page=" + i.ToString() 
            printfn "url = %s" url
            let page = fetchUrl url |> Async.RunSynchronously
            let imageUrls = getImageUrls page
            let getName (iurl:string) = 
                let s = iurl.Split '/'
                s.[s.Length-1]
         
            (* images in every search page *) 
            let images = 
                imageUrls 
               // |> Seq.map (fun url -> fetchUrl url) 
               // |> Async.Parallel
               // |> Async.RunSynchronously
                |> Seq.map (fun page -> 
                    async {
                        //let iurl, tags = getImageUrlAndTags page
                        //let icontent = getImage iurl |> Async.RunSynchronously
                        let icontent = getImage page |> Async.RunSynchronously
                        //let iname = getName iurl
                        //return iname, icontent, tags
                        return icontent
                    })
                |> Async.Parallel
                |> Async.RunSynchronously
            yield! images
     }

let downloadImagesWithTag (tag:string) (pages:int) (folder:string) = 
    let images = getImagesWithTag tag pages
    images
    //|> Seq.iter (fun (name, content, tags) -> 
    |> Seq.iteri (fun idx (content) -> 
        let fname = folder + idx.ToString() + ".jpg"
        File.WriteAllBytes(fname, content)
       // File.WriteAllLines(fname + ".tag", tags)
        )


[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    downloadImagesWithTag "cat" 1 @"C:\ImageData\flickr\sheep\"
    //let content = getImage "http://c4.staticflickr.com/4/3372/3418363599_fb8e571b45_n.jpg" |> Async.RunSynchronously
    //let fname = @"C:\ImageData\flickr\sheep\" + "a.jpg"
    //File.WriteAllBytes(fname, content)
    //File.WriteAllLines(fname + ".tag",[|"a";"b"|])
    0 // return an integer exit code
