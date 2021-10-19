
using System.Collections;
using IE.Model;
namespace IE.Facade
{
	
	public class GearFacade : BaseFacade
	{
		protected static GearFacade instance = new GearFacade(new GearModel());
		protected GearFacade(GearModel model) : base(model)
		{
		}
		public static GearFacade Instance
		{
			get { return instance; }
		}
		protected GearFacade():base() 
		{ 
		} 
	
	}
}
	