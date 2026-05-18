using System;
using System.Collections.Generic;
using System.Text;

namespace Legacy.Games
{
    internal interface IGame
    {
        /// <summary>
        /// 080000A0 - Game Title
        /// </summary>
        string GAME_CODE { get; }

        /// <summary>
        /// 080000BD - Complement Check
        /// </summary>
        int GAME_COMPLIMENT_CHECK { get; }

        bool IsGameValid(byte[] romHeader);

        void Load(byte[] rom);

        void Save(byte[] rom);

        //void ParseScript(byte[] data);
    }
}
