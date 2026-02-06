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

namespace EoS
open System
open System.Xml.Linq
open Eto
open FSharp.Data.UnitSystems.SI.UnitSymbols

/// Helper methods to draw anchored strings on a graphics context.
[<AutoOpen>]
module AlignedTextGraphics =
  type Drawing.Graphics with
    /// Draw text centered below the given point.
    member this.DrawTextCT(font:Drawing.Font, brush:Drawing.Brush, x:float32, y:float32, text:string) =
      let size = this.MeasureString(font, text)
      this.DrawText(font, brush, x - size.Width / 2.0f, y, text)

    /// Draw text vertically centered left of the given point.
    member this.DrawTextRM(font:Drawing.Font, brush:Drawing.Brush, x:float32, y:float32, text:string) =
      let size = this.MeasureString(font, text)
      this.DrawText(font, brush, x - size.Width, y - size.Height / 2.0f, text)

/// Annotated bitmap container.
type BitmapBox(bitmap:Drawing.Bitmap, colors:Map<int, int>, p0:float<Pa>, p1:float<Pa>, T0:float<K>, T1:float<K>) =
  inherit Forms.Drawable()

  member val Font = Drawing.Fonts.Sans(10.0f) with get, set
  member val AnnotationMargin = 40 with get, set
  member val PressureTicks = 11 with get, set
  member val TemperatureTicks = 11 with get, set

  member val Labels = Collections.Generic.List<int * int>()

  /// Paint the contents of the bitmap container on the given graphics context.
  member this.PaintOn(gfx : Drawing.Graphics, rect : Drawing.Rectangle) =
    let mutable rect = rect

    gfx.ImageInterpolation <- Drawing.ImageInterpolation.None

    rect.Inflate(-this.AnnotationMargin, -this.AnnotationMargin)
    gfx.DrawImage(bitmap, rect)

    for i in 0 .. this.PressureTicks-1 do
      let x = float32(rect.Left) + float32(i) * float32(rect.Width-1) / float32(this.PressureTicks-1)
      gfx.DrawLine(Drawing.Pens.Black, x, float32(rect.Bottom - 3), x, float32(rect.Bottom + 3))
      gfx.DrawLine(Drawing.Pens.Black, x, float32(rect.Top - 3), x, float32(rect.Top + 3))
      gfx.DrawTextCT(
        this.Font, Drawing.Brushes.Black, x, float32(rect.Bottom + 5),
        sprintf "%.*f" (if p1 - p0 > 1.0e9<Pa> then 0 else 2) <|
        (p0 + float i * (p1 - p0) / float(this.PressureTicks-1)) / 1.0e9<Pa>)

    for i in 0 .. this.TemperatureTicks-1 do
      let y = float32(rect.Bottom) - float32(i) * float32(rect.Height-1) / float32(this.TemperatureTicks-1)
      gfx.DrawLine(Drawing.Pens.Black, float32(rect.Left - 3), y, float32(rect.Left + 3), y)
      gfx.DrawLine(Drawing.Pens.Black, float32(rect.Right - 3), y, float32(rect.Right + 3), y)
      gfx.DrawTextRM(
        this.Font, Drawing.Brushes.Black, float32(rect.Left - 5), float32 y,
        sprintf "%.*f" (if T1 - T0 > 1.0<K> then 0 else 2) <|
        (T0 + float i * (T1 - T0) / float(this.TemperatureTicks-1)) / 1.0<K>)

    for x, y in this.Labels do
      let color =
        bitmap.GetPixel(x, y)
      let text =
        match colors.TryFind(color.ToArgb()) with
        | Some index -> string index
        | None -> "?"
      let pen, brush =
        if color.ToHSB().B > 0.5f then
          Drawing.Pens.Black, Drawing.Brushes.Black
        else
          Drawing.Pens.White, Drawing.Brushes.White
      let x, y =
        float32(rect.X + x * (rect.Width-1) / (bitmap.Width-1)),
        float32(rect.Y + y * (rect.Height-1) / (bitmap.Height-1))
      gfx.DrawEllipse(pen, x-2.0f, y-2.0f, 4.0f, 4.0f)
      gfx.DrawText(this.Font, brush, x + 1.0f, y + 1.0f, text)

  override this.OnPaint(evt) =
    base.OnPaint(evt)
    evt.Graphics.SetClip(evt.ClipRectangle)
    this.PaintOn(evt.Graphics, Drawing.Rectangle(Drawing.Point.Empty, this.ClientSize))

  /// Render an SVG representation of the plot.
  member this.ToSVG() =
    let svg = XNamespace.op_Implicit "http://www.w3.org/2000/svg"
    let lnk = XNamespace.op_Implicit "http://www.w3.org/1999/xlink"
    let ns0 = XNamespace.None

    let mutable rect = Drawing.Rectangle(Drawing.Point.Empty, this.ClientSize)
    rect.Inflate(-this.AnnotationMargin, -this.AnnotationMargin)

    let image =
      use buf = new IO.MemoryStream()
      bitmap.Save(buf, Drawing.ImageFormat.Png)
      XElement(
        svg + "image",
        XAttribute(ns0 + "id", "bitmap"),
        XAttribute(ns0 + "x", rect.X),
        XAttribute(ns0 + "y", rect.Y),
        XAttribute(ns0 + "width", rect.Width),
        XAttribute(ns0 + "height", rect.Height),
        XAttribute(ns0 + "preserveAspectRatio", "none"),
        XAttribute(
          lnk + "href",
          "data:image/png;base64," +
          Convert.ToBase64String(buf.ToArray())))

    let ticksP =
      XElement(
        svg + "g",
        XAttribute(ns0 + "id", "ticksP"),
        seq {
          for i in 0 .. this.PressureTicks-1 do
            let x = rect.Left + i * (rect.Width-1) / (this.PressureTicks-1)
            yield XElement(
              svg + "g",
              XElement(
                svg + "line",
                XAttribute(ns0 + "x1", x),
                XAttribute(ns0 + "y1", rect.Bottom - 3),
                XAttribute(ns0 + "x2", x),
                XAttribute(ns0 + "y2", rect.Bottom + 3),
                XAttribute(ns0 + "stroke", "black")))
            yield XElement(
              svg + "g",
              XElement(
                svg + "line",
                XAttribute(ns0 + "x1", x),
                XAttribute(ns0 + "y1", rect.Top - 3),
                XAttribute(ns0 + "x2", x),
                XAttribute(ns0 + "y2", rect.Top + 3),
                XAttribute(ns0 + "stroke", "black")),
              XElement(
                svg + "text",
                XAttribute(ns0 + "x", x),
                XAttribute(ns0 + "y", rect.Bottom + 5),
                XAttribute(ns0 + "text-anchor", "middle"),
                XAttribute(ns0 + "dy", 10),
                sprintf "%.*f" (if p1 - p0 > 1.0e9<Pa> then 0 else 2) <|
                (p0 + float i * (p1 - p0) / float(this.PressureTicks-1)) / 1.0e9<Pa>))
        })

    let ticksT =
      XElement(
        svg + "g",
        XAttribute(ns0 + "id", "ticksT"),
        seq {
          for i in 0 .. this.TemperatureTicks-1 do
            let y = rect.Bottom - i * (rect.Height-1) / (this.TemperatureTicks-1)
            yield XElement(
              svg + "g",
              XElement(
                svg + "line",
                XAttribute(ns0 + "x1", rect.Left - 3),
                XAttribute(ns0 + "y1", y),
                XAttribute(ns0 + "x2", rect.Left + 3),
                XAttribute(ns0 + "y2", y),
                XAttribute(ns0 + "stroke", "black")))
            yield XElement(
              svg + "g",
              XElement(
                svg + "line",
                XAttribute(ns0 + "x1", rect.Right - 3),
                XAttribute(ns0 + "y1", y),
                XAttribute(ns0 + "x2", rect.Right + 3),
                XAttribute(ns0 + "y2", y),
                XAttribute(ns0 + "stroke", "black")),
              XElement(
                svg + "text",
                XAttribute(ns0 + "x", rect.Left - 5),
                XAttribute(ns0 + "y", y),
                XAttribute(ns0 + "text-anchor", "end"),
                XAttribute(ns0 + "dy", 5),
                sprintf "%.*f" (if T1 - T0 > 1.0<K> then 0 else 2) <|
                (T0 + float i * (T1 - T0) / float(this.TemperatureTicks-1)) / 1.0<K>))
        })

    let ticked =
      XElement(
        svg + "g",
        XAttribute(ns0 + "id", "ticked"),
        image, ticksP, ticksT)

    let labels =
      XElement(
        svg + "g",
        XAttribute(ns0 + "id", "labels"),
        seq {
          for x, y in this.Labels do
            let color =
              bitmap.GetPixel(x, y)
            let text =
              match colors.TryFind(color.ToArgb()) with
              | Some index -> string index
              | None -> "?"
            let mark =
              if color.ToHSB().B > 0.5f then
                "black"
              else
                "white"
            let x, y =
              rect.X + x * (rect.Width-1) / (bitmap.Width-1),
              rect.Y + y * (rect.Height-1) / (bitmap.Height-1)
            yield XElement(
              svg + "g",
              XElement(
                svg + "circle",
                XAttribute(ns0 + "cx", x),
                XAttribute(ns0 + "cy", y),
                XAttribute(ns0 + "r", 2),
                XAttribute(ns0 + "fill", "none"),
                XAttribute(ns0 + "stroke", mark)),
              XElement(
                svg + "text",
                XAttribute(ns0 + "x", x),
                XAttribute(ns0 + "y", y),
                XAttribute(ns0 + "text-anchor", "start"),
                XAttribute(ns0 + "dx", 3),
                XAttribute(ns0 + "dy", 10),
                XAttribute(ns0 + "fill", mark),
                text))
        })

    XDocument(
      XElement(
        svg + "svg",
        XAttribute(ns0 + "version", "1.1"),
        XAttribute(ns0 + "width", sprintf "%dpx" this.Width),
        XAttribute(ns0 + "height", sprintf "%dpx" this.Height),
        ticked, labels))

  override this.OnMouseUp(evt) =
    let x, y =
      let mutable rect = Drawing.Rectangle(Drawing.Point.Empty, this.ClientSize)
      rect.Inflate(-this.AnnotationMargin, -this.AnnotationMargin)
      (evt.Location.X - float32 rect.X) * float32(bitmap.Width-1) / float32(rect.Width-1),
      (evt.Location.Y - float32 rect.Y) * float32(bitmap.Height-1) / float32(rect.Height-1)
    if 0.0f <= x && x < float32 bitmap.Width && 0.0f <= y && y < float32 bitmap.Height then
      this.Labels.Add(floor x |> int, floor y |> int)
      this.Invalidate()

    base.OnMouseUp(evt)
