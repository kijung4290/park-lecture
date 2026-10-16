using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ParkLecture
{
    internal sealed class FontChoice
    {
        internal string Name, Label;
        public override string ToString() { return Label; }
    }
    internal static class AnnotationFonts
    {
        internal const string Default = "Pretendard", Legacy = "Malgun Gothic";
        private static readonly PrivateFontCollection bundled = new PrivateFontCollection();
        private static readonly List<IntPtr> memory = new List<IntPtr>(), handles = new List<IntPtr>();
        static AnnotationFonts()
        {
            foreach (string weight in new[] { "Regular", "Bold" })
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ParkLecture.Pretendard." + weight + ".ttf"))
                {
                    if (stream == null) throw new InvalidOperationException("Bundled Pretendard font is missing.");
                    byte[] bytes = new byte[stream.Length]; int offset = 0, read;
                    while ((read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
                    IntPtr data = Marshal.AllocCoTaskMem(bytes.Length); Marshal.Copy(bytes, 0, data, bytes.Length); memory.Add(data);
                    bundled.AddMemoryFont(data, bytes.Length); uint count = 0; handles.Add(AddFontMemResourceEx(data, (uint)bytes.Length, IntPtr.Zero, ref count));
                }
            }
            AppDomain.CurrentDomain.ProcessExit += delegate
            { bundled.Dispose(); foreach (var handle in handles) if (handle != IntPtr.Zero) RemoveFontMemResourceEx(handle); foreach (var data in memory) Marshal.FreeCoTaskMem(data); };
        }
        private static readonly string[][] FreeFonts =
        {
            new[] { "나눔스퀘어", "NanumSquare", "NanumSquare_ac", "나눔스퀘어", "나눔스퀘어_ac" },
            new[] { "강원특별자치도체", "GANGWON STATE SemiBold", "GANGWON STATE", "강원특별자치도 SemiBold" },
            new[] { "배달의민족체", "BM DoHyeon", "BM JUA", "BM HANNA Pro", "배달의민족 도현", "배달의민족 주아", "배달의민족 한나Pro" }
        };
        internal static bool IsInstalled(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try { using (new FontFamily(name)) return true; } catch (ArgumentException) { return false; }
        }
        internal static Font Create(string name, float size, bool bold, GraphicsUnit unit = GraphicsUnit.Pixel)
        {
            FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular; size = Math.Max(8, Math.Min(1024, size));
            if (name != Default && IsInstalled(name))
                try { return new Font(name, size, style, unit); } catch (ArgumentException) { }
            return new Font(bundled.Families.First(f => f.Name == Default), size, style, unit);
        }
        internal static IEnumerable<FontChoice> Choices()
        {
            yield return new FontChoice { Name = Default, Label = "프리텐다드 (기본)" };
            foreach (var font in FreeFonts)
            {
                string installed = font.Skip(1).FirstOrDefault(IsInstalled);
                yield return new FontChoice { Name = installed ?? font[1], Label = font[0] + (installed == null ? Words.T(" (설치 필요)", " (not installed)") : "") };
            }
        }
        [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
        [DllImport("gdi32.dll")] private static extern bool RemoveFontMemResourceEx(IntPtr handle);
    }
}
