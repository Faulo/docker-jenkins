# Jenkins Agents as Docker Images

This repository contains Dockerfiles to build several different Jenkins agents. All of them can be used the same as jenkins/inbound-agent.

## basic-agent
This agent contains:
- A Java installation.
- A Git client.
- A Plastic client.

## ci-agent
This agent contains everything above as well as:
- A Steam client (steamcmd).
- An Itch.io client (butler).
- An installation of [slothsoft/unity](https://github.com/Faulo/slothsoft-unity) (compose-unity).

## unity-agent
This agent contains everything above as well as:
- A Unity Hub installation.
- A .NET installation.