// VEA.API/Services/ServiceService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;

namespace VEA.API.Services
{
    public class ServiceService
    {
        private readonly ApplicationDbContext _context;

        public ServiceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Service>> GetAllServicesAsync()
        {
            return await _context.Services
                .Include(s => s.Employees)
                .ToListAsync();
        }

        public async Task<Service?> GetServiceByIdAsync(int id)
        {
            return await _context.Services
                .Include(s => s.Employees)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Service> CreateServiceAsync(Service service)
        {
            _context.Services.Add(service);
            await _context.SaveChangesAsync();
            return service;
        }

        public async Task UpdateServiceAsync(Service service)
        {
            _context.Entry(service).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteServiceAsync(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
                return false;

            var idStr = id.ToString();
            var commaIdComma = $",{idStr},";

            bool hasPendingAppointments = await _context.Appointments
                .AnyAsync(a =>
                    (a.Status == "Pending" || a.Status == "Scheduled") &&
                    a.ServicesJson != null &&
                    (a.ServicesJson == idStr ||
                     a.ServicesJson.StartsWith(idStr + ",") ||
                     a.ServicesJson.EndsWith("," + idStr) ||
                     a.ServicesJson.Contains(commaIdComma)));

            if (hasPendingAppointments)
                throw new InvalidOperationException("Não é possível excluir o serviço porque ele está presente em agendamentos pendentes ou agendados.");

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ToggleServiceActiveAsync(int id, bool active)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
                throw new KeyNotFoundException("Serviço não encontrado.");

            service.Active = active;
            await _context.SaveChangesAsync();
        }

        public async Task AssignEmployeeToServiceAsync(int serviceId, int employeeId)
        {
            var service = await _context.Services
                .Include(s => s.Employees)
                .FirstOrDefaultAsync(s => s.Id == serviceId);

            var employee = await _context.Employees.FindAsync(employeeId);

            if (service == null || employee == null)
                throw new KeyNotFoundException("Serviço ou funcionário não encontrado.");

            if (!service.Employees.Any(e => e.Id == employeeId))
            {
                service.Employees.Add(employee);
                await _context.SaveChangesAsync();
            }
        }
    }
}