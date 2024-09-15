# CS2-HidePlayers

## Description
Allows you to hide player models.

## Requirments
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/)

## Installation
- Download the newest release from [Releases](https://github.com/qstage/CS2-HidePlayers/releases)
- Move the /gamedata folder to a folder /counterstrikesharp
- Make a folder in /plugins named /HidePlayers.
- Put the plugin files in to the new folder.
- Restart your server.

## Configuration
```json
{
  "Command": "css_hidemodels", // Command switch
  "Hidden": "@all", // Which players are hiding. Values: @all / @team / @enemy
  "Version": 1
}
```
