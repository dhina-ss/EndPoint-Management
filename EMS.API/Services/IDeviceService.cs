using EMS.API.DTOs;

namespace EMS.API.Services;

public interface IDeviceService
{
    Task<DeviceRegisterResponse> RegisterAsync(DeviceRegisterRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DeviceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
