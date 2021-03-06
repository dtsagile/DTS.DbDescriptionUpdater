DTS.DbDescriptionUpdater
========================

Database documentation tool for CodeFirst development. Adds MS_Descriptions to tables and columns so there is no need to manually add extended properties in MSSQL. Combine this with the power of 
<a href="https://github.com/jeremykdev/SqlServerDatabaseDocumentationGenerator">jeremykdev/SqlServerDatabaseDocumentationGenerator</a> and you have an fricken sweet documentation tool. 


<h1>Install</h1>
<p>Add reference to the DLL</p>

<h2>Configure</h2>
<p>Add DbDescriptionUpdater to Seed method of Migraitons/Configuration.cs</p>
<pre>
protected override void Seed(ConsoleApplication1.MyDbContext context)
{
    DbDescriptionUpdater&lt;MyDbContext&gt; updater = new DbDescriptionUpdater&lt;MyDbContext&gt;(context);
    updater.UpdateDatabaseDescriptions();
}
</pre>

<h2>Use</h2>
<h3>Add Descriptions to Models.</h3>
<pre>
    [DbTableMeta(Description = "Storage for person records.")]
    public class Person { }
</pre>

<h3>Add Descriptions to Model Properties.</h3>
<pre>
    [DbColumnMeta(Description = "Peron's first name.")]
    public string FirstName { get; set; }
</pre>

<h2>Requirements</h2>
<p>EntityFramework</p>

<p>That's it.</p>

<h2>Props</h2>
<p>I stumbled upon this class on <a href="http://stackoverflow.com/questions/10080601/how-to-add-description-to-columns-in-entity-framework-4-3-code-first-using-migra">StackOverflow</a>. Much of the original code exists. I added some additional work at the attributes.
