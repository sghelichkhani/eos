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

module EoS.BitmapTool
open System
open Eto
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Phases
open EoS.CommandLine

let init (comm : Lazy<EoS.MPI.Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"

  let database = Flag.DatabaseOpt "db" "Thermodynamic database"
  let pressure = Flag.PressureRange "P" "Pressure range"
  let pressureTicks = Flag.IntOpt "ticksP" 11 "Annotation ticks along the pressure axis"
  let temperature = Flag.TemperatureRange "T" "Temperature range"
  let temperatureTicks = Flag.IntOpt "ticksT" 11 "Annotation ticks along the temperature axis"

  let endmembers = Flag.BoolOpt "endmembers" "Use endmember rather than phase bitmaps"
  let useX = Flag.BoolOpt "useX" "Read composition vectors rather than bitmaps"

  let platform =
    let init =
      match Environment.OSVersion.Platform with
      | PlatformID.Unix ->
        Platforms.Gtk
      | PlatformID.MacOSX when Environment.Is64BitProcess ->
        Platforms.Mac64
      | _ ->
        Platforms.WinForms
    Flag.StringOpt "platform" init "GUI platform to use"

  let background =
    ValueFlag<Drawing.Color>(
      lazy Drawing.SystemColors.WindowBackground,
      "Color for unused pixels",
      Eto.Drawing.Color.Parse)
  Flag.Opts.Add("background", background)

  let width = Flag.IntOpt "width" 0 "Initial window width"
  let height = Flag.IntOpt "height" 0 "Initial window height"
  let margin = Flag.IntOpt "margin" 40 "Annotation margin"

  let font =
    ValueFlag<Drawing.Font>(
      lazy new Drawing.Font("sans-serif", 10.0f),
      "Font for annotations",
      fun str ->
        match str.LastIndexOf(' ') with
        | -1 -> new Drawing.Font(str, 10.0f)
        | sp -> new Drawing.Font(str[.. sp-1], float32 str[sp+1 ..]))
  Flag.Opts.Add("font", font)

  let rec readLines () =
    match stdin.ReadLine() with
    | null ->
      Seq.empty
    | line ->
      let line =
        let pos = line.IndexOf('#')
        (if pos >= 0 then line[0 .. pos-1] else line).Trim()
      if String.IsNullOrEmpty(line) then
        readLines ()
      else
        seq { yield SpaceRx.Split(line)
              yield! readLines () }

  let inRange (range:ValueRange<float<'u>>) (v:float<'u>) =
    let at = (v - range.Start.Value) / (range.Stop.Value - range.Start.Value)
    if 0.0 <= at && at <= 1.0 then
      Some (round (at * float(range.Steps.Value-1)) |> int)
    else
      None

  let hsvColor h s v =
    let h, c = h / 60.0, v * s
    let x = c * (1.0 - abs(h % 2.0 - 1.0))

    let r, g, b =
      if 0.0 <= h && h < 1.0 then
        c, x, 0.0
      elif 1.0 <= h && h < 2.0 then
        x, c, 0.0
      elif 2.0 <= h && h < 3.0 then
        0.0, c, x
      elif 3.0 <= h && h < 4.0 then
        0.0, x, c
      elif 4.0 <= h && h < 5.0 then
        x, 0.0, c
      elif 5.0 <= h && h < 6.0 then
        c, 0.0, x
      else
        0.0, 0.0, 0.0

    let m = v - c
    Drawing.Color.FromArgb(
      red = int((r + m) * 255.0),
      green = int((g + m) * 255.0),
      blue = int((b + m) * 255.0))

  let main args =
    Platform.Initialize(platform.Value)

    let blurb, phases =
      let db = database.Value
      match args with
      | [||] ->
        match db.TryGetObject<PhaseCollection>("ALL") with
        | Some phases -> "ALL", phases
        | None -> raise(FlagException("No default phase collection", String.Empty))
      | [|arg|] ->
        match db.TryGetObject<PhaseCollection>(arg) with
        | Some phases -> arg, phases
        | None -> raise(FlagException("Unknown phase collection", arg))
      | args ->
        args |> String.concat ", ",
        PhaseCollection(
          args
          |> Seq.map (fun arg ->
            match db.TryGetObject<IPhase>(arg) with
            | Some phase -> phase
            | None -> raise(FlagException("Unknown phase", arg))))

    let printHeader () =
      printfn "#Reading composition for %s phases" blurb
      stdout.Write('#')
      ["index"; "red"; "green"; "blue"; "description"]
      |> String.concat ofs.Value
      |> stdout.WriteLine

    let getInputs () =
      if useX.IsSet then
        if endmembers.IsSet then
          let rec bitmap ioffset (items : seq<PhaseCollectionItem>) x =
            items
            |> Seq.fold (fun bits it ->
              let ioffset = ioffset + it.XOffset
              let x = it.XSlice(x)
              match it.Phase with
              | :? seq<PhaseCollectionItem> as children ->
                bits ||| bitmap ioffset children x
              | _ ->
                bits ||| if Array.sum x > 0.0 then 1UL <<< ioffset else 0UL) 0UL
          seq { for x in readLines () do
                let p = float x[0] * 1.0<Pa>
                let T = float x[1] * 1.0<K>
                let x = Array.map float x[2..]
                yield (p, T, bitmap 0 phases x) }
        else
          seq { for x in readLines () do
                let p = float x[0] * 1.0<Pa>
                let T = float x[1] * 1.0<K>
                let x = Array.map float x[2..]
                let bits =
                  phases
                  |> Seq.fold (fun (bits, mask) it ->
                    (bits ||| if it.XSlice(x) |> Array.sum > 0.0 then mask else 0UL),
                    (mask <<< 1)) (0UL, 1UL)
                  |> fst
                yield (p, T, bits) }
      else
        seq { for x in readLines () do
              let p = float x[0] * 1.0<Pa>
              let T = float x[1] * 1.0<K>
              let bits = uint64 x[2]
              yield (p, T, bits) }

    let describePhases bits =
      (if endmembers.IsSet then
         let rec infoLoop ioffset (it : PhaseCollectionItem) =
           let ioffset = ioffset + it.XOffset
           match it.Phase with
           | :? seq<PhaseCollectionItem> as children ->
             Seq.collect (infoLoop ioffset) children
           | phase ->
             upcast [(bits >>> ioffset) &&& 1UL <> 0UL, phase]
         Seq.collect (infoLoop 0) phases
       else
         phases
         |> Seq.mapi (fun ioffset it ->
           (bits >>> ioffset) &&& 1UL <> 0UL, it.Phase))
      |> Seq.choose (fun (present, phase) ->
        if present then Some (string phase) else None)
      |> String.concat ", "

    let paintPixels (bitmap:Drawing.Bitmap) =
      let colormap =
        Collections.Generic.Dictionary<uint64, Drawing.Color * int>()

      let mutable hue, sat, vlu = 0.0, 0.0, 0.0
      for p, T, bits in getInputs () do
        match inRange pressure p, inRange temperature T with
        | Some pi, Some Ti ->
          let color =
            match colormap.TryGetValue(bits) with
            | true, (color, _) ->
              color
            | false, _ ->
              let index = colormap.Count
              let color =
                let sat = (sat + 27.0) / 100.0
                let vlu = (vlu + 1.0) * 0.33
                hsvColor hue sat vlu
              [string index
               string color.R
               string color.G
               string color.B
               describePhases bits]
              |> String.concat ofs.Value
              |> stdout.WriteLine
              colormap.Add(bits, (color, index))
              hue <- (hue + 89.0) % 360.0
              sat <- (sat + 7.0) % 73.0
              vlu <- (vlu + 1.0) % 3.0
              color
          bitmap.SetPixel(pi, bitmap.Height - 1 - Ti, color)
        | _ ->
          printfn "# p = %g Pa, T = %g K: Outside grid range" (p/1.0<Pa>) (T/1.0<K>)

      colormap.Values
      |> Seq.map (fun (color, index) -> color.ToArgb(), index)
      |> Map.ofSeq

    let okP = pressure.Start.IsSet && pressure.Stop.IsSet
    let okT = temperature.Start.IsSet && temperature.Stop.IsSet
    if okP && okT then
      use app = new Forms.Application()
      use form = new Forms.Form(Title = "BitmapViewer")
      do
        let mutable size = form.ClientSize
        if width.IsSet then size.Width <- width.Value
        if height.IsSet then size.Height <- height.Value
        form.ClientSize <- size

      let bitmap =
        new Drawing.Bitmap(
          pressure.Steps.Value, temperature.Steps.Value,
          Drawing.PixelFormat.Format24bppRgb)
      do
        use gfx = new Drawing.Graphics(bitmap)
        gfx.Clear(background.Value)

      printHeader ()
      let colors = paintPixels bitmap

      let bbox =
        new BitmapBox(
          bitmap = bitmap, colors = colors,
          p0 = pressure.Start.Value, p1 = pressure.Stop.Value,
          PressureTicks = pressureTicks.Value,
          T0 = temperature.Start.Value, T1 = temperature.Stop.Value,
          TemperatureTicks = temperatureTicks.Value,
          AnnotationMargin = margin.Value, Font = font.Value)

      let undo =
        Forms.Command(
          (fun _ _ ->
            let n = bbox.Labels.Count
            if n > 0 then
              bbox.Labels.RemoveAt(n-1)
              bbox.Invalidate()),
          MenuText = "&Undo", Shortcut = (app.CommonModifier ||| Forms.Keys.Z))

      let clear =
        Forms.Command(
          (fun _ _ ->
            bbox.Labels.Clear()
            bbox.Invalidate()),
          MenuText = "&Clear")

      let save =
        Forms.Command(
          (fun _ _ ->
            use dialog = new Forms.SaveFileDialog()
            dialog.Filters.Add(Forms.FileFilter("Scalable Vector Graphics", ".svg"))
            dialog.Filters.Add(Forms.FileFilter("Portable Network Graphics", ".png"))
            dialog.Filters.Add(Forms.FileFilter("Graphics Interchange Format", ".gif"))
            dialog.Filters.Add(Forms.FileFilter("Tagged Image File", ".tif", ".tiff"))
            dialog.Filters.Add(Forms.FileFilter("Joint Photographic Experts Group", ".jpg", ".jpeg"))
            dialog.Filters.Add(Forms.FileFilter("All Image Files", ".svg", ".png", ".gif", ".tif", ".tiff", ".jpg", ".jpeg"))
            dialog.Filters.Add(Forms.FileFilter("All Files"))
            dialog.CurrentFilterIndex <- 1
            if dialog.ShowDialog(form) = Forms.DialogResult.Ok then
              let format = IO.Path.GetExtension(dialog.FileName).ToLowerInvariant()
              if format = ".svg" then
                bbox.ToSVG().Save(dialog.FileName)
              else
                use bitmap = new Drawing.Bitmap(bbox.Width, bbox.Height, Drawing.PixelFormat.Format24bppRgb)
                do
                  use gfx = new Drawing.Graphics(bitmap)
                  gfx.Clear(background.Value)
                  bbox.PaintOn(gfx, Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height))
                let format =
                  match format with
                  | ".png" -> Drawing.ImageFormat.Png
                  | ".gif" -> Drawing.ImageFormat.Gif
                  | ".tif" | ".tiff" -> Drawing.ImageFormat.Tiff
                  | ".jpg" | ".jpeg" -> Drawing.ImageFormat.Jpeg
                  | _ -> Drawing.ImageFormat.Png
                bitmap.Save(dialog.FileName, format)
            ()),
          MenuText = "&Save", Shortcut = (app.CommonModifier ||| Forms.Keys.S))

      let quit =
        Forms.Command(
          (fun _ _ -> form.Close()),
          MenuText = "&Quit", Shortcut = (app.CommonModifier ||| Forms.Keys.Q))
      
      let fileMenu = new Forms.ButtonMenuItem(Text = "&File")
      fileMenu.Items.Add(save) |> ignore
      fileMenu.Items.Add(quit) |> ignore
      
      let editMenu = new Forms.ButtonMenuItem(Text = "&Edit")
      editMenu.Items.Add(undo) |> ignore
      editMenu.Items.Add(clear) |> ignore

      form.Menu <- new Forms.MenuBar(fileMenu, editMenu)
      form.Content <- bbox

      app.Run(form)

      seq {
        for x, y in bbox.Labels do
          let color = bitmap.GetPixel(x, y)
          match colors.TryFind(color.ToArgb()) with
          | Some index -> yield index
          | None -> ()
      }
      |> Set.ofSeq
      |> Seq.map string
      |> String.concat ", "
      |> printfn "# Labelled %s"

      0

    else
      seq {
        if not okP then yield "pressure"
        if not okT then yield "temperature"
      }
      |> String.concat " and "
      |> eprintfn "Missing %s range"

      1
    
  main
