#define _CRT_SECURE_NO_DEPRECATE
#include <iostream>
#include <string.h>

using namespace std;

class CompressionSourceData
{
private:
	const unsigned* Data;
	unsigned        BitBuffer;
	int             BitsRemaining;

public:
	CompressionSourceData(const unsigned* iData)
	{
		Data = iData;
		BitBuffer = 0;
		BitsRemaining = 0;
	}

	int GetBit()
	{
		if (!BitsRemaining)
		{
			BitsRemaining = 32;
			BitBuffer = *Data++;
		}
		int ReturnValue = BitBuffer >> 31;
		BitBuffer <<= 1;
		BitsRemaining--;
		return ReturnValue;
	}

	int GetBits(int Count)
	{
		if (BitsRemaining >= Count)
		{
			int ReturnValue = BitBuffer >> (32 - Count);
			BitBuffer <<= Count;
			BitsRemaining -= Count;
			return ReturnValue;
		}
		else
		{
			int Remainder = Count - BitsRemaining;
			int ReturnValue = BitBuffer >> (32 - BitsRemaining) << Remainder;
			BitBuffer = *Data++;
			ReturnValue |= BitBuffer >> (32 - Remainder);
			BitsRemaining = 32 - Remainder;
			BitBuffer <<= Remainder;
			return ReturnValue;
		}
	}

	int GetInteger()
	{
		int Value = 1;
		do
		{
			Value = (Value << 1) + GetBit();
		} while (GetBit());
		return Value;
	}
};

struct CompressionState
{
	int LastIndex;
	int IndexBase;
	int LiteralBits;
	int LiteralOffset;

	CompressionState() { LastIndex = 1; IndexBase = 8; LiteralBits = 0; LiteralOffset = 0; }
};

void TransferMatch(unsigned char*& Destination, int MatchOffset, int MatchLength)
{
	unsigned char* p = Destination;
	unsigned char* s = p - MatchOffset;
	do
	{
		*p++ = *s++;
	} while (--MatchLength);
	Destination = p;
}

int DecompressJCALG1(void* pDestination, const unsigned* pSource)
{
	CompressionState      State;
	CompressionSourceData Source(pSource);
	unsigned char* Destination = (unsigned char*)pDestination;
	unsigned char* DestStart = Destination;

	while (1)
	{
		if (Source.GetBit())
		{
			*Destination++ = Source.GetBits(State.LiteralBits) + State.LiteralOffset;
		}
		else
		{
			if (Source.GetBit())
			{
				int HighIndex = Source.GetInteger();
				if (HighIndex == 2)
				{
					int PhraseLength = Source.GetInteger();
					TransferMatch(Destination, State.LastIndex, PhraseLength);
				}
				else
				{
					State.LastIndex = ((HighIndex - 3) << State.IndexBase) + Source.GetBits(State.IndexBase);
					int PhraseLength = Source.GetInteger();
					if (State.LastIndex >= 0x10000) PhraseLength += 3;
					else if (State.LastIndex >= 0x37FF)  PhraseLength += 2;
					else if (State.LastIndex >= 0x27F)   PhraseLength += 1;
					else if (State.LastIndex <= 127)     PhraseLength += 4;
					TransferMatch(Destination, State.LastIndex, PhraseLength);
				}
			}
			else
			{
				if (Source.GetBit())
				{
					int OneBytePhraseValue = Source.GetBits(4) - 1;
					if (OneBytePhraseValue == 0)
					{
						*Destination++ = 0;
					}
					else if (OneBytePhraseValue > 0)
					{
						*Destination = *(Destination - OneBytePhraseValue);
						Destination++;
					}
					else
					{
						if (Source.GetBit())
						{
							do
							{
								for (int i = 0; i < 256; i++)
									*Destination++ = Source.GetBits(8);
							} while (Source.GetBit());
						}
						else
						{
							State.LiteralBits = 7 + Source.GetBit();
							State.LiteralOffset = 0;
							if (State.LiteralBits != 8)
								State.LiteralOffset = Source.GetBits(8);
						}
					}
				}
				else
				{
					int NewIndex = Source.GetBits(7);
					int MatchLength = 2 + Source.GetBits(2);
					if (NewIndex == 0)
					{
						if (MatchLength == 2) break;
						State.IndexBase = Source.GetBits(MatchLength + 1);
					}
					else
					{
						State.LastIndex = NewIndex;
						TransferMatch(Destination, State.LastIndex, MatchLength);
					}
				}
			}
		}
	}

	return (int)(Destination - DestStart);
}

int main()
{

	
	uint8_t portrait_bytes[] = { 1,   0,   0,   0,   0,0x10,   0,   0,0xDE,0x93,

	};

	uint8_t* pCompressed = portrait_bytes + 8;
	uint32_t uncompressedSize = *(uint32_t*)(portrait_bytes + 4);

	unsigned char* d = new unsigned char[uncompressedSize]();

	int out_len = DecompressJCALG1(d, (const unsigned*)pCompressed);

	printf("Decompressed %d bytes (expected %d)\n", out_len, uncompressedSize);

	FILE* f = fopen("output.bin", "wb");
	fwrite(d, 1, out_len, f);
	fclose(f);

	printf("Written to output.bin\n");
	delete[] d;
	return 0;

	return 0;
}