﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public static class GitWrapper
    {
        public static IEnumerable<GitCommit> CommittedVersions(string repo, string git_install)
        {
            string[] all = CommandRunner.RunCommand(repo, $"\"{git_install}\" log --all --pretty=\"%H___%s___%ad___%p\" --date=format:\"%Y/%m/%d\"", output: true).Output.Split('\n');
            foreach (var item in all)
            {
                if (String.IsNullOrEmpty(item))
                    continue;
                var entries = item.Split("___");
                if (String.IsNullOrEmpty(entries[3]))
                    continue;
                yield return new GitCommit(entries[0], entries[1], DateTime.Parse(entries[2]));
            }
        }
    }

    public class GitCommit
    {
        public readonly string Hash;
        public readonly string Message;
        public readonly DateTime CommitTime;
        public GitCommit(string hash, string message, DateTime time)
        {
            Hash = hash;
            Message = message;
            CommitTime = time;
        }
    }
}