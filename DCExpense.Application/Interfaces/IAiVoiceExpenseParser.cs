using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;

namespace DCExpense.Application.Interfaces;

public interface IAiVoiceExpenseParser
{
    ExpenseParsedResultDto ParseExpenseText(string text);
    Task<ExpenseParsedResultDto> ParseVoiceAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}
