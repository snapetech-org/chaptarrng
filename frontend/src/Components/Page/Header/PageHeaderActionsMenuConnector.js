import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { saveUISettings, setUISettingsValue } from 'Store/Actions/settingsActions';
import { restart, shutdown } from 'Store/Actions/systemActions';
import PageHeaderActionsMenu from './PageHeaderActionsMenu';

function createMapStateToProps() {
  return createSelector(
    (state) => state.system.status,
    (status) => {
      return {
        formsAuth: status.item.authentication === 'forms'
      };
    }
  );
}

class PageHeaderActionsMenuConnector extends Component {

  //
  // Listeners

  onRestartPress = () => {
    this.props.restart();
  };

  onShutdownPress = () => {
    this.props.shutdown();
  };

  onExperienceModePress = () => {
    const experienceMode = this.props.experienceMode === 'simple' ? 'advanced' : 'simple';
    this.props.setUISettingsValue({ name: 'experienceMode', value: experienceMode });
    this.props.saveUISettings();
  };

  //
  // Render

  render() {
    return (
      <PageHeaderActionsMenu
        {...this.props}
        onRestartPress={this.onRestartPress}
        onShutdownPress={this.onShutdownPress}
        onExperienceModePress={this.onExperienceModePress}
      />
    );
  }
}

PageHeaderActionsMenuConnector.propTypes = {
  restart: PropTypes.func.isRequired,
  shutdown: PropTypes.func.isRequired,
  experienceMode: PropTypes.oneOf(['simple', 'advanced']).isRequired,
  setUISettingsValue: PropTypes.func.isRequired,
  saveUISettings: PropTypes.func.isRequired
};

const mapDispatchToProps = {
  restart,
  shutdown,
  setUISettingsValue,
  saveUISettings
};

export default connect(createMapStateToProps, mapDispatchToProps)(PageHeaderActionsMenuConnector);
