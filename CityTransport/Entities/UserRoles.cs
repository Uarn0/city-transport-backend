namespace CityTransport.Entities;

public static class UserRoles
{
    // 1. Роль: Пасажир (Passenger / User)
    public const string Passenger = "Passenger";
    public const string User = "User"; // Аліас для зворотної сумісності

    // 2. Роль: Контролер / Водій / Кондуктор (Controller / Driver / Inspector)
    public const string Controller = "Controller";
    public const string Driver = "Driver";
    public const string Inspector = "Inspector";

    // 3. Роль: Адміністратор / Перевізник (Admin / Carrier)
    public const string Admin = "Admin";
    public const string Carrier = "Carrier";

    // Складені списки ролей для атрибутів [Authorize(Roles = ...)]
    public const string PassengerOrAdmin = "Passenger,User,Admin,Carrier";
    public const string ControllerOrAdmin = "Controller,Driver,Inspector,Admin,Carrier";
    public const string AdminOnly = "Admin,Carrier";
    public const string AllRoles = "Passenger,User,Controller,Driver,Inspector,Admin,Carrier";
}
