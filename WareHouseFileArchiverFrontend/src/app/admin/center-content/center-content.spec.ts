import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CenterContentComponent } from './center-content';

describe('CenterContent', () => {
  let component: CenterContentComponent;
  let fixture: ComponentFixture<CenterContentComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CenterContentComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CenterContentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
