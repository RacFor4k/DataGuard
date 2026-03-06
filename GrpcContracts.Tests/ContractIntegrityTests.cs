using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using GrpcContracts.Account;
using ProtoBuf;
using ProtoBuf.Meta;
using Xunit;

namespace GrpcContracts.Tests;

public class ContractIntegrityTests
{
    private const string SnapshotFile = "contract-snapshot.json";

    [Fact]
    public void AllContractTypes_ShouldHaveProtoContractAttribute()
    {
        // Arrange
        var contractTypes = GetContractTypes();

        // Act & Assert
        foreach (var type in contractTypes)
        {
            var protoContractAttr = type.GetCustomAttribute<ProtoContractAttribute>();
            protoContractAttr.Should().NotBeNull(
                $"Тип {type.Name} должен иметь атрибут [ProtoContract]");
        }
    }

    [Fact]
    public void AllProtoMembers_ShouldHaveUniqueNumbers()
    {
        // Arrange
        var contractTypes = GetContractTypes();

        // Act & Assert
        foreach (var type in contractTypes)
        {
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ProtoMemberAttribute>() != null)
                .ToList();

            var fields = type.GetFields()
                .Where(f => f.GetCustomAttribute<ProtoMemberAttribute>() != null)
                .ToList();

            var allMembers = properties.Select(p => (MemberInfo)p)
                .Concat(fields.Select(f => (MemberInfo)f))
                .ToList();

            var protoNumbers = allMembers
                .Select(m =>
                {
                    var attr = m.GetCustomAttribute<ProtoMemberAttribute>();
                    return attr?.Tag;
                })
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            protoNumbers.Count.Should().Be(protoNumbers.Distinct().Count(),
                $"В типе {type.Name} все ProtoMember должны иметь уникальные номера");
        }
    }

    [Fact]
    public void AllProtoMembers_ShouldHavePositiveNumbers()
    {
        // Arrange
        var contractTypes = GetContractTypes();

        // Act & Assert
        foreach (var type in contractTypes)
        {
            var members = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ProtoMemberAttribute>() != null)
                .Cast<MemberInfo>()
                .Concat(type.GetFields()
                    .Where(f => f.GetCustomAttribute<ProtoMemberAttribute>() != null)
                    .Cast<MemberInfo>())
                .ToList();

            foreach (var member in members)
            {
                var attr = member.GetCustomAttribute<ProtoMemberAttribute>();
                attr.Should().NotBeNull();
                attr!.Tag.Should().BeGreaterThan(0,
                    $"ProtoMember {member.Name} в типе {type.Name} должен иметь положительный номер");
            }
        }
    }

    [Fact]
    public void ContractSnapshot_ShouldMatchPreviousVersion()
    {
        // Arrange
        var currentSnapshot = CreateSnapshot();
        var previousSnapshot = LoadPreviousSnapshot();

        // Act & Assert
        if (previousSnapshot != null)
        {
            // Проверяем что все предыдущие контракты сохранились
            foreach (var previousType in previousSnapshot.Types)
            {
                var currentType = currentSnapshot.Types
                    .FirstOrDefault(t => t.Name == previousType.Name);

                currentType.Should().NotBeNull(
                    $"Тип {previousType.Name} был удалён из контрактов");

                // Проверяем что все ProtoMember сохранились с теми же номерами
                foreach (var previousMember in previousType.Members)
                {
                    var currentMember = currentType.Members
                        .FirstOrDefault(m => m.Name == previousMember.Name);

                    currentMember.Should().NotBeNull(
                        $"Поле/свойство {previousMember.Name} было удалено из типа {previousType.Name}");

                    currentMember!.ProtoMemberNumber.Should().Be(previousMember.ProtoMemberNumber,
                        $"ProtoMember номер для {previousType.Name}.{previousMember.Name} изменился с {previousMember.ProtoMemberNumber} на {currentMember.ProtoMemberNumber}");
                }
            }

            // Проверяем что не было удалено никаких ProtoMember (даже если тип остался)
            foreach (var currentType in currentSnapshot.Types)
            {
                var previousType = previousSnapshot.Types
                    .FirstOrDefault(t => t.Name == currentType.Name);

                if (previousType != null)
                {
                    foreach (var currentMember in currentType.Members)
                    {
                        var previousMember = previousType.Members
                            .FirstOrDefault(m => m.Name == currentMember.Name);

                        // Если поле было в предыдущей версии, проверяем номер
                        // Если поля не было - это новое поле, что допустимо
                        if (previousMember != null)
                        {
                            currentMember.ProtoMemberNumber.Should().Be(previousMember.ProtoMemberNumber,
                                $"ProtoMember номер для {currentType.Name}.{currentMember.Name} изменился");
                        }
                    }
                }
            }
        }

        // Сохраняем новый snapshot
        SaveSnapshot(currentSnapshot);
    }

    [Fact]
    public void SerializationDeserialization_ShouldPreserveData()
    {
        // Arrange & Act & Assert для каждого типа контракта
        TestRoundTrip(new SignUpRequest { Name = "Test", Surname = "User", Email = "test@example.com", PublicKey = "key123", SignUpToken = "token123" });
        TestRoundTrip(new SignInRequest { PublicKey = "key123" });
        TestRoundTrip(new SignInResponce { Name = "Test", Surname = "User", Email = "test@example.com", EncryptedToken = "enc", SeccionId = "session123", Groups = new List<KeyValuePair<int, string>> { new(1, "Admin") } });
        TestRoundTrip(new CreateCompanyRequest { CreationToken = "create123" });
        TestRoundTrip(new CreateCompanyResponce { Token = "response123" });
        TestRoundTrip(new LiquidateCompanyRequest { Token = "liquidate123", Type = Status.Active });
        TestRoundTrip(new LiquidateCompanyConfirmRequest { key = 42 });
        
        // Пустые объекты тестируем без сравнения содержимого
        TestRoundTripEmpty(new LiquidateCompanyResponce());
        TestRoundTripEmpty(new SignUpResponce());
    }

    private void TestRoundTrip<T>(T original) where T : class, new()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, original);
        ms.Position = 0;
        var deserialized = Serializer.Deserialize<T>(ms);

        deserialized.Should().BeEquivalentTo(original, options => options
            .RespectingRuntimeTypes()
            .AllowingInfiniteRecursion());
    }

    private void TestRoundTripEmpty<T>(T original) where T : class, new()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, original);
        ms.Position = 0;
        var deserialized = Serializer.Deserialize<T>(ms);

        deserialized.Should().NotBeNull();
    }

    private List<Type> GetContractTypes()
    {
        return typeof(IAccountServise).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() != null)
            .ToList();
    }

    private ContractSnapshot CreateSnapshot()
    {
        var snapshot = new ContractSnapshot
        {
            Types = GetContractTypes()
                .Select(t => new TypeSnapshot
                {
                    Name = t.Name,
                    FullName = t.FullName!,
                    Members = t.GetProperties()
                        .Where(p => p.GetCustomAttribute<ProtoMemberAttribute>() != null)
                        .Select(p => new MemberSnapshot
                        {
                            Name = p.Name!,
                            ProtoMemberNumber = p.GetCustomAttribute<ProtoMemberAttribute>()!.Tag,
                            TypeName = p.PropertyType.Name
                        })
                        .Concat(t.GetFields()
                            .Where(f => f.GetCustomAttribute<ProtoMemberAttribute>() != null)
                            .Select(f => new MemberSnapshot
                            {
                                Name = f.Name!,
                                ProtoMemberNumber = f.GetCustomAttribute<ProtoMemberAttribute>()!.Tag,
                                TypeName = f.FieldType.Name
                            }))
                        .OrderBy(m => m.ProtoMemberNumber)
                        .ToList()
                })
                .OrderBy(t => t.Name)
                .ToList()
        };

        return snapshot;
    }

    private ContractSnapshot? LoadPreviousSnapshot()
    {
        var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SnapshotFile);
        if (!File.Exists(snapshotPath))
        {
            return null;
        }

        var json = File.ReadAllText(snapshotPath);
        return JsonSerializer.Deserialize<ContractSnapshot>(json);
    }

    private void SaveSnapshot(ContractSnapshot snapshot)
    {
        var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SnapshotFile);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(snapshot, options);
        File.WriteAllText(snapshotPath, json);
    }
}

public class ContractSnapshot
{
    public List<TypeSnapshot> Types { get; set; } = new();
}

public class TypeSnapshot
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<MemberSnapshot> Members { get; set; } = new();
}

public class MemberSnapshot
{
    public string Name { get; set; } = "";
    public int ProtoMemberNumber { get; set; }
    public string TypeName { get; set; } = "";
}
