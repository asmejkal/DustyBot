﻿using System;

namespace DustyBot.Database.Mongo.Collections.GreetBye.Models
{
    public class GreetByeEmbed
    {
        public string? Title { get; set; }
        public Uri? Image { get; set; }
        public string Body { get; set; }
        public int? Color { get; set; }
        public string? Footer { get; set; }
        public string? Text { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public GreetByeEmbed()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public GreetByeEmbed(string body, string? title = null, Uri? image = null, int? color = null, string? footer = null, string? text = null)
        {
            Title = title;
            Image = image;
            Body = !string.IsNullOrEmpty(body) ? body : throw new ArgumentException("body");
            Color = color;
            Footer = footer;
            Text = text;
        }
    }
}