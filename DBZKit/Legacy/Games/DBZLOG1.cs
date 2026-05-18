using System;
using System.Collections.Generic;
using System.Text;

namespace Legacy.Games
{
    internal class DBZLOG1 : IGame
    {
        ///<inheritdoc/>
        public string GAME_CODE => "";

        ///<inheritdoc/>
        public int GAME_COMPLIMENT_CHECK => 0x0;

        ///<inheritdoc/>
        public bool IsGameValid(byte[] romHeader)
        {
            return false;
        }

        ///<inheritdoc/>
        public void Load(byte[] rom)
        {
            throw new NotImplementedException();
        }

        ///<inheritdoc/>
        public void Save(byte[] rom)
        {
            throw new NotImplementedException();
        }
    }
}
