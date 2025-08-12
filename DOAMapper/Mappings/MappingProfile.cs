using AutoMapper;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.Enums;

namespace DOAMapper.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Player, PlayerDto>()
            .ForMember(dest => dest.AllianceName, opt => opt.MapFrom(src => src.Alliance != null ? src.Alliance.Name : null))
            .ForMember(dest => dest.DataDate, opt => opt.MapFrom(src => src.ValidFrom))
            .ForMember(dest => dest.Rank, opt => opt.Ignore()) // Rank is set manually in the service
            .ForMember(dest => dest.CityX, opt => opt.Ignore())
            .ForMember(dest => dest.CityY, opt => opt.Ignore());

        CreateMap<Player, PlayerDetailDto>()
            .IncludeBase<Player, PlayerDto>()
            .ForMember(dest => dest.TileCount, opt => opt.MapFrom(src => src.Tiles.Count))
            .ForMember(dest => dest.TilesByType, opt => opt.MapFrom(src => 
                src.Tiles.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Count())));
                
        CreateMap<Alliance, AllianceDto>()
            .ForMember(dest => dest.MemberCount, opt => opt.MapFrom(src => src.Members.Count))
            .ForMember(dest => dest.DataDate, opt => opt.MapFrom(src => src.ValidFrom));
            
        CreateMap<Tile, TileDto>()
            .ForMember(dest => dest.PlayerName, opt => opt.MapFrom(src => src.Player != null ? src.Player.Name : null))
            .ForMember(dest => dest.AllianceName, opt => opt.MapFrom(src => src.Alliance != null ? src.Alliance.Name : null))
            .ForMember(dest => dest.DataDate, opt => opt.MapFrom(src => src.ValidFrom));
            
        CreateMap<ImportSession, ImportSessionDto>();

        CreateMap<Realm, RealmDto>();
    }
}
