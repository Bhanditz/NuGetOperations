[![internal JetBrains project](http://jb.gg/badges/internal-flat-square.svg)](https://confluence.jetbrains.com/display/ALL/JetBrains+on+GitHub)

# NuGet Gallery Operations Toolkit

This is a set of operations tools designed for the official NuGet.org site. Its **sole** purpose is to make it easier for the NuGet Gallery team to maintain the site. This is **not** a general purpose operations toolkit and is not designed to work with every NuGetGallery installation. It works for us and we thought it might help those of you with private galleries, so we have open-sourced it. We'll do our best to help you use and configure it, but making this user-friendly is not our top priority :). We are very unlikely to take Pull Requests that don't directly enhance our workflows, but please do feel free to make and maintain forks for your own purposes and contribute back general-purpose changes that you think we might find helpful as well.

## What's inside
This repo contains NuGet Gallery Operations tools. These tools include:

1. A PowerShell Console Environment for working with deployed versions of the NuGet Gallery
2. An Azure Worker Role which performs database backups and statistics processing.
3. An Operations application which can perform ops tasks such as deleting packages, adding them to curated feeds, managing backups, etc.
4. A set of Monitoring components used to monitor the status of the gallery and its associated resources (SQL Databases, Blob storage, etc.)

## Getting Started
Getting started with this toolkit is easy. Make sure you have git installed (duh) and clone the repo. Then run "NuGetOps.cmd" to start the console. Create a shortcut to NuGetOps.cmd for easy access (there's even an icon included which you can use for the console).
