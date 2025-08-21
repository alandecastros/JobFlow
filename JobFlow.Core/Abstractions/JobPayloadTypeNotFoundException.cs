using System;

namespace JobFlow.Core;

public class JobPayloadTypeNotFoundException(string workerId, string payloadType)
    : Exception($"{workerId} can't find payload type {payloadType}");