using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeaBattleGame
{
    enum PackageType
    {
        Start,
        Turn,
        Check,
        Next,
        Ready,
        Result,
    }

    enum GameResult
    {
        none,
        lose,
        win,
    }
}
//Start;
//Turn;i = 0-9;j = 0-9
//Check;win,lose,end,none
//Next;
//Ready;0-server;1-client
//Result;field = 0-4;i = 0-9; j = 0-9