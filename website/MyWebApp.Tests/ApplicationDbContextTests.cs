using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;
using MyWebApp.Models;
using Xunit;

public class ApplicationDbContextTests
{
    [Fact]
    public void CanAddAndRetrieveRecording()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "RecordingsDb")
            .Options;

        // Insert a recording
        using (var context = new ApplicationDbContext(options))
        {
            context.Recordings.Add(new Recording { Name = "Test", Created = DateTime.UtcNow });
            context.SaveChanges();
        }

        // Retrieve recording
        using (var context = new ApplicationDbContext(options))
        {
            var recording = context.Recordings.Single();
            Assert.Equal("Test", recording.Name);
        }
    }
}
